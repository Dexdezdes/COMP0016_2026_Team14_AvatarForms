
from agents import Model, TalkerAgent, EvaluatorAgent, RAG_Agent
from sockets import stream_message, start_server, websocket_handler
from formatting import bcolors

import os
import asyncio
from dotenv import load_dotenv
load_dotenv()
# LOCAL_API_URL
# FIREWORKS_API_KEY

class AvatarFormsInterviewer:
    def __init__(self, is_local=False, cloud_model=None, cutoff=4):
        self.cutoff = cutoff

        self.user_role = "user"
        self.AI_role = "assistant"

        self.is_local = is_local
        self.cloud_model = cloud_model


    def build_interview(self, questions, interview_context):
        self.questions = questions
        self.interview_context = interview_context

        self.questions_index = 0
        self.conversation_history = []
        self.question_labels = [] # Each entry in conversation_history is linked to a question
        self.last_evaluation = None

        self.model = self.get_model()

        self.talker = TalkerAgent(
            model=self.model,
            conversation_history=self.conversation_history,
            interview_context=self.interview_context)
        
        self.evaluator = EvaluatorAgent(
            model=self.model,
            interview_context=self.interview_context)
        
        self.rag_agent = RAG_Agent(
            model=self.model,
            interview_context=self.interview_context)
        
    def reset_interview(self):
        self.questions_index = 0
        self.conversation_history.clear() # not resetting to empty list to preserve reference for agents
        self.question_labels.clear()
        self.last_evaluation = None

    def build_from_json(self, json):
        self.build_interview(json["questions"], json["description"])

    
    def get_model(self):
        params = {
            "max_tokens": 4096,
            "top_p": 1,
            "top_k": 40,
            "presence_penalty": 0,
            "frequency_penalty": 0,
            "temperature": 0.6,
            "stream": False
        }
        if self.is_local:
            url = os.getenv("LOCAL_API_URL")
            if not url:
                raise ValueError("LOCAL_API_URL environment variable not set")

            return Model(url=url, model=self.cloud_model, api_key=None, params=params)
        
        else:
            if not self.cloud_model:
                self.cloud_model = "accounts/fireworks/models/qwen3-vl-235b-a22b-instruct"
            
            api_key = os.getenv("FIREWORKS_API_KEY")
            if not api_key:
                raise ValueError("FIREWORKS_API_KEY environment variable not set")
            return Model(url="https://api.fireworks.ai/inference/v1/chat/completions", model=self.cloud_model, api_key=api_key, params=params)
        
    def get_conversation_section(self, question_index):
        section = []
        for i, label in enumerate(self.question_labels):
            if label == question_index:
                section.append(self.conversation_history[i])
        return section
    
    def should_cutoff(self):
        # If we've been on current question for too long, cutoff and move on
        return len(self.get_conversation_section(self.questions_index)) >= self.cutoff*2 # Each question has 2 messages (question and answer)


    def start_interview(self):
        self.reset_interview()

        # Ask the first question
        question = self.questions[self.questions_index]
        question_speech = self.talker.ask_question(question)

        self.conversation_history.append({"role": self.AI_role, "content": question_speech})
        self.question_labels.append(self.questions_index)

        return question_speech

    def respond(self, response):
        question = self.questions[self.questions_index]
        self.conversation_history.append({"role": self.user_role, "content": response})
        self.question_labels.append(self.questions_index)

        evaluation = self.evaluator.evaluate(question, self.get_conversation_section(self.questions_index))
        self.last_evaluation = evaluation
        
        if self.should_cutoff() or evaluation["satisfactory"] or evaluation["override_skip"]:
            self.questions_index += 1
            self.last_evaluation = None
        if evaluation["override_skip"]:
            self.conversation_history.append({"role": "system", "content": f"Question '{question}' skipped by user preference. Moved on to question {self.questions[self.questions_index]}."})

        if self.questions_index < len(self.questions):
            question = self.questions[self.questions_index]
            if not self.last_evaluation:
                question_speech = self.talker.ask_question(question)
            else:
                question_speech = self.talker.ask_followup(question, self.last_evaluation["reasoning"], self.last_evaluation.get("follow_up_question"))
            
            self.conversation_history.append({"role": self.AI_role, "content": question_speech})
            self.question_labels.append(self.questions_index)

            return question_speech, True
        
        else:
            closing_statement = self.talker.closing_statement()
            self.conversation_history.append({"role": self.AI_role, "content": closing_statement})
            self.question_labels.append(self.questions_index+1)

            return closing_statement, False
        
    def collect_final_answers(self):
        final_answers = {}
        for i, question in enumerate(self.questions):
            conversation_section = self.get_conversation_section(i)
            answer = self.rag_agent.answer(question, conversation_section)
            final_answers[question] = answer
        
        return final_answers
        
    # Kind of obsolete, good for testing
    def run_interview_whole(self, verbose=True):
        # If verbose, output of all agents will be printed, otherwise only the talker is printed.

        # During interview
        while self.questions_index < len(self.questions):

            question = self.questions[self.questions_index]
            if verbose:
                print(f"\n{bcolors.HEADER}Question {self.questions_index + 1}: {question}{bcolors.ENDC}")
            
            # If a new question needs to be asked, ask the main question. Otherwise, ask the follow-up question.
            if not self.last_evaluation:
                question_speech = self.talker.ask_question(question)
                print(f"{bcolors.OKBLUE}Talker: {question_speech}{bcolors.ENDC}")

            else:
                question_speech = self.talker.ask_followup(question, self.last_evaluation["reasoning"], self.last_evaluation.get("follow_up_question"))
                print(f"{bcolors.OKBLUE}Talker (Follow-up): {question_speech}{bcolors.ENDC}")
            
            self.conversation_history.append({"role": self.AI_role, "content": question_speech})
            self.question_labels.append(self.questions_index)

            answer = input(f"User: ")
            self.conversation_history.append({"role": self.user_role, "content": answer})
            self.question_labels.append(self.questions_index)

            evaluation = self.evaluator.evaluate(question, self.get_conversation_section(self.questions_index))
            self.last_evaluation = evaluation

            if verbose:
                # print(f"{bcolors.WARNING}{Evaluator}: Satisfactory: {evaluation['satisfactory']}, Override Skip: {evaluation['override_skip']}, Reasoning: {evaluation['reasoning']}, Follow-up Question: {evaluation.get('follow_up_question', "None")}{bcolors.ENDC}")
                print(f"{bcolors.WARNING}Evaluator: {evaluation}{bcolors.ENDC}")
        
            if self.should_cutoff() or evaluation["satisfactory"] or evaluation["override_skip"]:
                self.questions_index += 1
                self.last_evaluation = None

                if verbose and self.should_cutoff():
                    print(f"{bcolors.WARNING}Cutoff reached for question {self.questions_index + 1}. Moving to next question.{bcolors.ENDC}")
                    # print(f"{bcolors.WARNING}Moving to question {self.questions_index + 1}.{bcolors.ENDC}")
            
                if evaluation["override_skip"]:
                    self.conversation_history.append({"role": "system", "content": f"Question '{question}' skipped by user preference. Moved on to question {self.questions[self.questions_index]}."})

        closing_statement = self.talker.closing_statement()
        print(f"{bcolors.OKBLUE}Talker (Closing Statement): {closing_statement}{bcolors.ENDC}")
        self.conversation_history.append({"role": self.AI_role, "content": closing_statement})
        self.question_labels.append(self.questions_index+1)

        # After interview, use RAG agent to collate
        print(f"\n{bcolors.HEADER}Interview complete. Finalizing answers...{bcolors.ENDC}")
        final_answers = self.collect_final_answers()
        for question, answer in final_answers.items():
            print(f"{bcolors.OKGREEN}Q: {question}{bcolors.ENDC}")
            print(f"A: {answer}\n")

        return final_answers

async def main():
    # Start WebSocket server
    server = await start_server()

    # Setup interview
    questions = [
        "What is your full name?",
        "How did you sleep last night?",
        "Do you generally sleep well?",
        "How are you feeling today?",
    ]

    interview_context = "This questionnaire is designed to get complete information about the user in a friendly manner and get to know them."
    interviewer = AvatarFormsInterviewer(is_local=False, cutoff=4)
    interviewer.build_interview(questions, interview_context)

    # Start interview
    first_question = interviewer.start_interview()
    print(f"{bcolors.OKBLUE}Talker: {first_question}{bcolors.ENDC}")
    await stream_message("speech", first_question)

    # Interview loop
    while True:
        response = await asyncio.to_thread(input, "User: ")
        # print(interviewer.conversation_history)
        speech_line, continue_interview = interviewer.respond(response)
        print(f"{bcolors.OKBLUE}Talker: {speech_line}{bcolors.ENDC}")  # Output to UI in production
        await stream_message("speech", speech_line)

        if not continue_interview:
            print(f"{bcolors.OKGREEN}Interview complete.{bcolors.ENDC}")
            final_answers = interviewer.collect_final_answers()
            # Dict of questions and answers
            for question, answer in final_answers.items():
                print(f"{bcolors.OKGREEN}Q: {question}{bcolors.ENDC}")
                print(f"A: {answer}\n")
                break

        # Keep server running after interview completes
        print(f"\n{bcolors.OKGREEN}Server will continue running. Press Ctrl+C to stop.{bcolors.ENDC}")
        try:
            await asyncio.Future()
        except asyncio.CancelledError:
            pass


### Example usage ###
if __name__ == "__main__":
    # Run async main with WebSocket support
    asyncio.run(main())

    # # OR use synchronous version (comment out asyncio.run(main()) above):
    # questions = [
    #     "What is your name?",
    #     "Describe your hometown.",
    #     "Describe a challenging situation you have faced and how you handled it.",
    #     "What are your hobbies and interests?",
    # ]
    # questions = [
    #     "What is your full name?",
    #     "How did you sleep last night?",
    #     # "Do you generally sleep well?",
    #     # "How are you feeling today?",
    # ]

    # interview_context = "This questionnaire is designed to get complete information about the user in a friendly manner and get to know them."

    # interviewer = AvatarFormsInterviewer(is_local=False, cutoff=4)
    # interviewer.build_interview(questions, interview_context)

    # # Start interview
    # first_question = interviewer.start_interview()
    # print(f"Talker: {first_question}")
    # while True:
    #     response = input("User: ") # Replace with actual user input in production
    #     # print(interviewer.conversation_history)
    #     speech_line, continue_interview = interviewer.respond(response)
    #     print(f"Talker: {speech_line}") # Output to UI in production

    #     if not continue_interview:
    #         print("Interview complete.")
    #         final_answers = interviewer.collect_final_answers()
    #         # Dict of questions and answers
    #         for question, answer in final_answers.items():
    #             print(f"{bcolors.OKGREEN}Q: {question}{bcolors.ENDC}")
    #             print(f"A: {answer}\n")
    #         break 