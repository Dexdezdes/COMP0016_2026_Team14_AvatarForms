import os
import json
import time

class LLM_Trace:
    def __init__(self, agent_name: str, prompt: str, output: str, parameters: dict = None, response_time: float = None) -> None:
        self.agent_name = agent_name
        self.prompt = prompt
        self.output = output
        self.parameters = parameters
        self.response_time = response_time

    def to_dict(self) -> dict:
        return {
            "agent": self.agent_name,
            "prompt": self.prompt,
            "output": self.output,
            "parameters": self.parameters,
            "response_time": self.response_time
        }

class Tracer:
    def __init__(self, log_dir: str, log_name: str = None, print_logs: bool = False, model_params: dict = None) -> None:
        self.logs = []
        self.log_dir = log_dir
        self.print_logs = print_logs
        self.model_params = model_params if model_params is not None else {}
        if log_name is None:
            log_name = f"trace_{int(time.time())}.json"
        self.log_path = os.path.join(self.log_dir, log_name)

        if not os.path.exists(self.log_dir):
            os.makedirs(self.log_dir)
        if not os.path.exists(self.log_path):
            with open(self.log_path, 'w') as f:
                f.write("")

    
    def log(self, agent_name: str, prompt: str, output: str, temperature: float = None, response_time: float = None) -> None:
        if temperature is not None:
            params = self.model_params.copy()
            params["temperature"] = temperature
        else:
            params = self.model_params

        trace = LLM_Trace(agent_name, prompt, output, params, response_time)
        if self.print_logs:
            print(f"Agent: {agent_name}")
            print(f"Input: {prompt}")
            print(f"Output: {output}")
            # print(f"Parameters: {params}")
            print(f"Response Time: {response_time:.2f} seconds")
            print("-" * 50)
        self.logs.append(trace)
        self.write_log(trace)

    def write_log(self, trace: LLM_Trace) -> None:
        log_entry = trace.to_dict()
        with open(self.log_path, 'a') as f:
            f.write(json.dumps(log_entry) + "\n")

