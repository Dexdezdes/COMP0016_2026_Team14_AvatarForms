from sockets import stream_message, start_server, websocket_handler, wait_for_browser_connection
from api import start_http_server, wait_for_questionnaire, send_response
from formatting import bcolors, format_question, match_mcq_option
import os
import asyncio
import csv
import argparse

from agents import Model, TalkerAgent, EvaluatorAgent, RAG_Agent, Question

from dotenv import load_dotenv
load_dotenv()
# LOCAL_API_URL
# FIREWORKS_API_KEY

# Llamafile should be in the same directory as this file

class AvatarFormsInterviewer:
    def __init__(self, is_local: bool = False, model_name: str = None, local_port: int = 8081, cutoff: int = 4) -> None:
        self.cutoff = cutoff

        self.user_role = "user"
        self.AI_role = "assistant"

        self.is_local = is_local
        if self.is_local:
            self.local_port = local_port
        self.model_name = model_name


    def build_interview(self, questions: list, interview_context: str) -> None:
        self.questions = self.load_questions(questions)
        self.interview_context = interview_context

        self.questions_index = 0
        self.conversation_history = []
        self.question_labels = [] # Each entry in conversation_history is linked to a question
        self.last_evaluation = None

        self.answers = [""]*len(self.questions) # Final answers to each question for output at

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
        
    def reset_interview(self) -> None:
        self.questions_index = 0
        self.conversation_history.clear() # not resetting to empty list to preserve reference for agents
        self.question_labels.clear()
        self.last_evaluation = None

    def build_from_json(self, json) -> None:
        self.build_interview(json["questions"], json["description"])
    
    def load_questions(self, questions: list) -> list[Question]:
        if isinstance(questions[0], dict):
            self.questions = [Question.from_dict(q) for q in questions]
        elif isinstance(questions[0], str):
            self.questions = [Question(text=q) for q in questions]
        elif isinstance(questions[0], Question):
            self.questions = questions
        return self.questions


    def get_model(self) -> Model:
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
            # Use the llamafile URL and endpoint
            url = f"http://127.0.0.1:{self.local_port}/v1/chat/completions"
            if not self.model_name:
                self.model_name = "LLaMA_CPP"
            api_key = None
        
        else:
            url = "https://api.fireworks.ai/inference/v1/chat/completions"
            if not self.model_name:
                self.model_name = "accounts/fireworks/models/qwen3-vl-235b-a22b-instruct"
            
            api_key = os.getenv("FIREWORKS_API_KEY")
            if not api_key:
                raise ValueError("FIREWORKS_API_KEY environment variable not set")
            
        return Model(url=url, model=self.model_name, api_key=api_key, params=params)
        
    def get_conversation_section(self, question_index: int) -> list:
        section = []
        for i, label in enumerate(self.question_labels):
            if label == question_index:
                section.append(self.conversation_history[i])
        return section

    def should_cutoff(self) -> bool:
        # If we've been on current question for too long, cutoff and move on
        return len(self.get_conversation_section(self.questions_index)) >= self.cutoff*2 # Each question has 2 messages (question and answer)

    def start_interview(self) -> str:
        self.reset_interview()

        # Ask the first question
        question = format_question(self.questions[self.questions_index])
        question_speech = self.talker.ask_question(question, previous_q_and_a=None)

        self.conversation_history.append({"role": self.AI_role, "content": question_speech})
        self.question_labels.append(self.questions_index)

        return question_speech

    def respond(self, response: str) -> tuple[str, bool]:
        question = format_question(self.questions[self.questions_index])
        self.conversation_history.append({"role": self.user_role, "content": response})
        self.question_labels.append(self.questions_index)

        evaluation = self.evaluator.evaluate(question, self.get_conversation_section(self.questions_index))
        self.last_evaluation = evaluation

        # print(self.question_labels)
        # print(self.get_conversation_section(self.questions_index))

        if self.should_cutoff() or evaluation["satisfactory"] or evaluation["override_skip"]:
            self.questions_index += 1
            self.last_evaluation = None

            self.questions[self.questions_index-1].answer = self.collect_answer(self.questions_index-1)

        if evaluation["override_skip"]:
            self.conversation_history.append({"role": "system", "content": f"Question '{question['text']}' skipped by user preference. Moved on to question {self.questions[self.questions_index]}."})

        if self.questions_index < len(self.questions):
            question = format_question(self.questions[self.questions_index])

            q_and_as = self.collect_all_answers()
            if all(answer == "" for answer in q_and_as.values()):
                q_and_as = None

            if not self.last_evaluation: # If no evaluation, this is a new question, so ask the main question. Otherwise, ask the follow-up question.
                question_speech = self.talker.ask_question(question, previous_q_and_a=q_and_as)
            else:
                transcript = self.get_conversation_section(self.questions_index)
                question_speech = self.talker.ask_followup(question, self.last_evaluation["reasoning"], transcript, previous_q_and_a=q_and_as, follow_up=self.last_evaluation.get("follow_up_question"))

            self.conversation_history.append({"role": self.AI_role, "content": question_speech})
            self.question_labels.append(self.questions_index)

            return question_speech, True
        
        else:
            closing_statement = self.talker.closing_statement()
            self.conversation_history.append({"role": self.AI_role, "content": closing_statement})
            self.question_labels.append(self.questions_index+1)

            return closing_statement, False
    
    def collect_answer(self, question_index: int) -> str:
        conversation_section = self.get_conversation_section(question_index)

        question = format_question(self.questions[question_index])
        question_type = self.questions[question_index].question_type
        options = self.questions[question_index].options if question_type == "mcq" else None
        answer = self.rag_agent.answer(question, conversation_section, question_type=question_type, options=options)

        # For MCQ questions, try to match the answer to one of the valid options
        if question_type == "mcq" and options:
            answer = match_mcq_option(answer, options)

        return answer

    def collect_all_answers(self) -> dict:
        final_answers = {}
        for i, question in enumerate(self.questions):
            if question.answer is not None:
                final_answers[question.text] = question.answer
            else:
                final_answers[question.text] = self.collect_answer(i)
        return final_answers

    def output_to_csv(self, filename: str, final_answers: dict) -> None:
        with open(filename, mode='w', newline='', encoding='utf-8') as csv_file:
            writer = csv.writer(csv_file)
            writer.writerow(["Question", "Answer"])
            for question, answer in final_answers.items():
                writer.writerow([question, answer])
        

async def main():
    parser = argparse.ArgumentParser(description="Run the AvatarForms interview backend server.")
    parser.add_argument("--local", action="store_true", help="Use local model (LLaMA_CPP) instead of Fireworks API")
    parser.add_argument("--llama_port", type=int, default=8081, help="Port for local model server if --local is set (default: 8081)")
    parser.add_argument("-p", "--port", type=int, default=8883, help="Port for the WebSocket server (default: 8883)")
    parser.add_argument("--http_port", type=int, default=8882, help="Port for the HTTP API server (default: 8882)")
    parser.add_argument("--response_port", type=int, default=5000, help="Port for the Response API service (default: 5000)")
    args = parser.parse_args()

    #Start HTTP Server
    start_http_server(args.http_port)
    questionnaire_data = await wait_for_questionnaire()

    # Start WebSocket server
    server = await start_server(args.port)

    # Wait for browser (HeadTTS) to connect
    await wait_for_browser_connection()

    # Setup interview after browser is connected
    interviewer = AvatarFormsInterviewer(is_local=args.local, local_port=args.llama_port, cutoff=4)
    interviewer.build_from_json(questionnaire_data)

    # Start interview
    first_question = interviewer.start_interview()
    print(f"{bcolors.OKBLUE}Talker: {first_question}{bcolors.ENDC}")
    await stream_message("speech", first_question)

    # Interview loop
    while True:
        response = await asyncio.to_thread(input)
        # print(interviewer.conversation_history)
        speech_line, continue_interview = interviewer.respond(response)
        print(f"{bcolors.OKBLUE}Talker: {speech_line}{bcolors.ENDC}")
        await stream_message("speech", speech_line)

        if not continue_interview:
            print(f"{bcolors.OKGREEN}Interview complete.{bcolors.ENDC}")
            final_answers = interviewer.collect_all_answers()
            # Dict of questions and answers
            for i, (question, answer) in enumerate(final_answers.items()):
                print(f"{bcolors.OKGREEN}Q: {question}{bcolors.ENDC}")
                print(f"A: {answer}\n")

                # Send each response back to ResponseAPIService
                questionnaire_id = questionnaire_data["questionnaire_id"]
                question_type = interviewer.questions[i].get("type", "open_ended")
                options = interviewer.questions[i].get("options")

                # For MCQ, selected_option is the matched option text
                selected_option = answer if question_type == "mcq" else None

                send_response(
                    questionnaire_id=questionnaire_id,
                    question_order=i + 1,
                    question=question,
                    answer=answer,
                    port=args.response_port,
                    question_type=question_type,
                    selected_option=selected_option
                )

            break

    # Close the WebSocket server
    print(f"\n{bcolors.OKGREEN}Closing WebSocket server...{bcolors.ENDC}")
    server.close()
    await server.wait_closed()
    print(f"{bcolors.OKGREEN}Server closed.{bcolors.ENDC}")


### Example usage ###
if __name__ == "__main__":
    # Run async main with WebSocket support
    asyncio.run(main())

    # # OR use synchronous version (comment out asyncio.run(main()) above):
    # # questions = [
    # #     "What is your name?",
    # #     "Describe your hometown.",
    # #     "Describe a challenging situation you have faced and how you handled it.",
    # #     "What are your hobbies and interests?",
    # # ]
    # questions = [
    #     "What is your full name?",
    #     "How did you sleep last night?",
    #     "Do you generally sleep well?",
    #     "How are you feeling today?",
    #     "What is your favourite movie?",
    # ]

    # interview_context = "This questionnaire is designed to get complete information about the user in a friendly manner and get to know them."

    # interviewer = AvatarFormsInterviewer(is_local=False, cutoff=4, model_name="accounts/fireworks/models/qwen3-8b")
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
    #         final_answers = interviewer.collect_all_answers()
    #         # Dict of questions and answers
    #         print(interviewer.answers)
    #         for question, answer in final_answers.items():
    #             print(f"{bcolors.OKGREEN}Q: {question}{bcolors.ENDC}")
    #             print(f"A: {answer}\n")
    #         break 