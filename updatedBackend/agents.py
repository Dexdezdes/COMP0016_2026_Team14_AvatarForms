
import requests
import json

from formatting import clean_script, outputToJSON, conversationToText
from prompts import Talker_system_prompt, Talker_ask_question_prompt, Talker_follow_up_question_prompt, Talker_closing_statement_prompt,Evaluator_system_prompt, RAG_system_prompt

class Model:
    def __init__(self, url, model, api_key=None, params=None):
        self.url = url
        self.model = model
        self.api_key = api_key
        self.params = params if params is not None else {
            "max_tokens": 4096,
            "top_p": 1,
            "top_k": 40,
            "presence_penalty": 0,
            "frequency_penalty": 0,
            "temperature": 0.6,
            "stream": False
        }

    def generate(self, messages, temperature=None):
        # Call the model's API with the messages and return the response
        payload = {
            "model": self.model,
            **self.params,
            "messages": messages
        }
        if temperature is not None:
            payload["temperature"] = temperature

        headers = {
            "Accept": "application/json",
            "Content-Type": "application/json",
        }

        if self.api_key:
            headers["Authorization"] = f"Bearer {self.api_key}"
        response = requests.request("POST", self.url, headers=headers, data=json.dumps(payload))
        if response.status_code == 200:
            return response.json()
        else:
            raise Exception(f"API call failed with status code {response.status_code}: {response.text}")
        


class Agent:
    def __init__(self, model):
        self.model = model

    def run(self, messages, temperature=None):
        response = self.model.generate(messages, temperature=temperature)
        return response["choices"][0]["message"]["content"]
        
    

class TalkerAgent(Agent):
    def __init__(self, model, conversation_history, interview_context):
        super().__init__(model)
        self.conversation_history = conversation_history
        self.interview_context = interview_context

        self.system_prompt = Talker_system_prompt(interview_context)
        self.ask_question_prompt = Talker_ask_question_prompt
        self.follow_up_question_prompt = Talker_follow_up_question_prompt
        self.closing_statement_prompt = Talker_closing_statement_prompt()

    def ask_question(self, question):
        task_prompt = self.ask_question_prompt(question)
        system = self.system_prompt + task_prompt
        messages = [{"role": "system", "content": system}] + self.conversation_history
        # messages.append({"role": "system", "content": task_prompt})
        # print(conversationToText(messages))
        output = self.run(messages)
        return clean_script(output)

    def ask_followup(self, question, reasoning, follow_up=None):
        task_prompt = self.follow_up_question_prompt(question, reasoning, follow_up)
        system = self.system_prompt + task_prompt
        messages = [{"role": "system", "content": system}] + self.conversation_history
        # messages.append({"role": "system", "content": task_prompt})
        # print(conversationToText(messages))
        output = self.run(messages)
        return clean_script(output)
    
    def closing_statement(self):
        system = self.system_prompt + self.closing_statement_prompt
        messages = [{"role": "system", "content": system}]# + self.conversation_history
        output = self.run(messages)
        return clean_script(output)

class EvaluatorAgent(Agent):
    def __init__(self, model, interview_context):
        super().__init__(model)
        self.prompt = Evaluator_system_prompt
        self.interview_context = interview_context

    def evaluate(self, question, conversation_history):
        transcript = conversationToText(conversation_history)
        system = self.prompt(self.interview_context, question, transcript)
        messages = [{"role": "system", "content": system}]
        output = self.run(messages, temperature=0.2)
        return outputToJSON(output)
        
    
class RAG_Agent(Agent):
    def __init__(self, model, interview_context):
        super().__init__(model)
        self.interview_context = interview_context

    def answer(self, question, conversation_history):
        transcript = conversationToText(conversation_history)
        system = RAG_system_prompt(self.interview_context, transcript, question)
        messages = [{"role": "system", "content": system}]
        output = self.run(messages, temperature=0.1)
        return output.strip()