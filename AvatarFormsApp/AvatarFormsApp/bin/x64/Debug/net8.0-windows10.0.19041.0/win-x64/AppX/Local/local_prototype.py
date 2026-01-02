import os
from dotenv import load_dotenv
import ollama

load_dotenv()

class Agent:
    def __init__(self, name, model, params, system_prompt):
        self.name = name
        self.model = model
        self.params = params
        self.system_prompt = system_prompt
        self.messages = [{"role": "system", "content": system_prompt}]

class Interview:
    def __init__(self, questions, context=None):
        self.questions = questions
        self.context = context
        self.requirements = []
        self.answers = []
        self.history = []

class bcolors:
    HEADER = '\033[95m'
    OKBLUE = '\033[94m'
    OKCYAN = '\033[96m'
    OKGREEN = '\033[92m'
    WARNING = '\033[93m'
    FAIL = '\033[91m'
    ENDC = '\033[0m'
    BOLD = '\033[1m'
    UNDERLINE = '\033[4m'

OLLAMA_HOST = os.getenv("OLLAMA_HOST")

if OLLAMA_HOST and not OLLAMA_HOST.startswith("http"):
    OLLAMA_HOST = f"http://{OLLAMA_HOST}"

if OLLAMA_HOST:
    print(f"Connecting to Ollama host: {OLLAMA_HOST}")
    ollama.host = OLLAMA_HOST

default_params = {
    "num_predict": 64,
    "temperature": 0.1,
    "top_p": 1.0,
    "repeat_penalty": 1.0,
    "num_ctx": 1024,
}

def updateTemperature(base, temperature):
    updated = base.copy()
    updated["temperature"] = temperature
    return updated

def thinkStrip(text):
    tag = ["<think>", "</think>"]
    if tag[0] in text and tag[1] in text:
        start = text.index(tag[0])
        end = text.index(tag[1]) + len(tag[1])
        text = text[:start] + text[end:]
    return text.strip()

def runAgent(agent, messages):
    full_messages = agent.messages + messages

    response = ollama.chat(
        model=agent.model,
        messages=full_messages,
        options=agent.params
    )

    text = response["message"]["content"]
    return thinkStrip(text)

Talker = Agent(
    name="Talker",
    model="gemma3:1b",
    params=default_params,
    system_prompt="You are a straightforward AI interviewer. Your job is to ask questions in a concise but friendly manner, not wasting the time of the respondent by being overly verbose. You adapt your tone based on the context provided and the user's previous answers. Always be polite, respect privacy and don't pry."
)

Nitpicker = Agent(
    name="Nitpicker",
    model="gemma3:1b",
    params=default_params,
    system_prompt="You are a detail-oriented judge who decides whether or not an answer provides complete information to a given question and is satisfactory or if a follow-up question / clarification is required. You adhere to the requirements provided but can be flexible based on context."
)

RequirementDefiner = Agent(
    name="RequirementDefiner",
    model="gemma3:1b",
    params=default_params,
    system_prompt="You are an AI that defines very short requirements for a satisfactory answer to a question. You will be provided questionnaire questions that are to be asked by an interviewer, to be answered by the user."
)

testInterview = Interview(
    questions=[
        "What is your name?",
        "What A levels did you do?",
        "What is your favorite food?"
    ],
    context="This questionnaire is designed to get complete information about the user in a friendly manner and get to know them."
)

def setupInterview(interview):
    for question in interview.questions:
        req_message = [
            {"role": "system", "content": f"A verbal interview is being conducted with the context: '{interview.context}' \nIn this context, define the information the user would need to provide for a complete satisfactory answer to the question: '{question}'. Write a short set of validation criteria."}
        ]
        requirements = runAgent(RequirementDefiner, req_message)
        interview.requirements.append(requirements)

    Talker.system_prompt += f"You are currently conducting a verbal interview with the context: '{interview.context}'"

def askQuestion(interview, index, question_override=None):
    if question_override:
        question = question_override
    else:
        question = interview.questions[index]
    requirements = interview.requirements[index]

    # Talker asks the question
    talker_message = interview.history + [
        {"role": "system", "content": f"Ask the following question: '{question}'"}
    ]

    asked_question = runAgent(Talker, talker_message)
    interview.history.append({"role": "assistant", "content": asked_question})
    print(f"{bcolors.OKBLUE}Talker: {asked_question}{bcolors.ENDC}", flush=True)

    # User provides an answer
    answer = input("User: ")
    interview.history.append({"role": "user", "content": answer})
    # Nitpicker evaluates the answer
    nitpicker_message = interview.history + [
        {"role": "system", "content": f"Evaluate the answer to the previous question based on these requirements: '{requirements}'. If not complete, return only a rephrased or follow-up question. If complete, return only the word 'CONTINUE'. If the user is distressed or wants to skip the question, CONTINUE."}
    ]

    evaluation = runAgent(Nitpicker, nitpicker_message)
    print(f"{bcolors.WARNING}Critic: {evaluation}{bcolors.ENDC}", flush=True)

    #For very small models that do not know how to only output CONTINUE
    if "continue" in evaluation.strip().lower():
    #if evaluation.strip().lower() == "continue":
        process_message = interview.history + [
            {"role": "system", "content": f"Based on the requirements {requirements} to the question '{question}', finalize and format the answer provided by the user. Do not change the meaning of the answer or add any new information whatsoever. Return only the finalized answer."}
        ]
        processed_answer = runAgent(RequirementDefiner, process_message)
        interview.answers.append(processed_answer)

    return evaluation

setupInterview(testInterview)

for i in range(len(testInterview.questions)):
    satisfactory = False
    question_override = None
    while not satisfactory:
        evaluation = askQuestion(testInterview, i, question_override=question_override)
        if "continue" in evaluation.lower():
        #if evaluation.lower() == "continue":
            satisfactory = True
            print(f"{bcolors.OKGREEN}Answer accepted.{bcolors.ENDC}\n", flush=True)
        else:
            question_override = evaluation

print(f"{bcolors.HEADER}Interview complete. Answers:{bcolors.ENDC}")
for idx, ans in enumerate(testInterview.answers):
    print(f"{bcolors.HEADER}Q: {testInterview.questions[idx]}{bcolors.ENDC}", flush=True)
    print(f"{bcolors.HEADER}A: {ans}{bcolors.ENDC}", flush=True)