import sys
import timeit
import json

sys.path.append("..\\Backend")  # Add parent directory to sys.path to allow imports from Backend

from agents import Question, Model, TalkerAgent, EvaluatorAgent, RAG_Agent
from formatting import conversationToText, format_question
from deepeval.test_case import LLMTestCase, LLMTestCaseParams

class TestCase:
    def __init__(self, name: str):
        self.name = name


class EvaluatorTestCase(TestCase):
    """
    Test case for evaluating the EvaluatorAgent.
    """
    def __init__(self, name: str, interview_context: str, conversation_history: list, question: Question, expected_answer: dict):
        super().__init__(name)
        self.interview_context = interview_context
        self.conversation_history = conversation_history
        self.question = question if isinstance(question, Question) else Question(text=question)

        self.expected_answer = expected_answer

    def input_str(self):
        """Format the input for the test case."""
        return f"""Interview context:
{self.interview_context}
Conversation history:
{conversationToText(self.conversation_history)}
Question:
{format_question(self.question)}
"""

    def run(self, model: Model, evaluator_agent: EvaluatorAgent) -> dict:
        """Run the test case using the provided EvaluatorAgent."""
        evaluator = evaluator_agent(model, interview_context=self.interview_context)
        return evaluator.evaluate(
            question=format_question(self.question), 
            conversation_history=self.conversation_history
        )
    
    def deepeval_testcase(self, model: Model, evaluator_agent: EvaluatorAgent) -> LLMTestCase:
        """Convert to DeepEval test case format."""
        timeit_start = timeit.default_timer()
        output = self.run(model, evaluator_agent)
        elapsed = timeit.default_timer() - timeit_start
        
        return LLMTestCase(
            name=self.name,
            input=self.input_str(),
            expected_output=json.dumps(self.expected_answer),
            actual_output=json.dumps(output),
            retrieval_context=[self.interview_context],
            completion_time=elapsed
        )


class TalkerTestCase(TestCase):
    def __init__(self, name: str, interview_context: str, conversation_history: list, question: Question, expected_answer: str, previous_q_and_a: dict = None, follow_up: bool = False, reasoning: str = None, expected_follow_up: str = None):
        super().__init__(name)
        self.interview_context = interview_context
        self.conversation_history = conversation_history
        self.question = question if isinstance(question, Question) else Question(text=question)
        self.expected_answer = expected_answer
        self.previous_q_and_a = previous_q_and_a
        self.follow_up = follow_up
        self.reasoning = reasoning
        self.expected_follow_up = expected_follow_up

    def input_str(self):
        return f"""Interview context: {self.interview_context}
Conversation history: {conversationToText(self.conversation_history)}
Question: {format_question(self.question)}"""

    def run(self, model: Model, talker_agent: TalkerAgent) -> str:
        """Run the test case using the provided talker_agent."""
        talker = talker_agent(model, interview_context=self.interview_context, conversation_history=self.conversation_history)
        if self.follow_up:
            return talker.ask_followup(
                question=format_question(self.question),
                reasoning=self.reasoning,
                transcript=self.conversation_history,
                previous_q_and_a=self.previous_q_and_a,
                follow_up=self.expected_follow_up
            )
        else:
            return talker.ask_question(
                question=format_question(self.question), 
                previous_q_and_a=self.previous_q_and_a
            )
    
    def deepeval_testcase(self, model: Model, talker_agent: TalkerAgent) -> LLMTestCase:
        timeit_start = timeit.default_timer()
        output = self.run(model, talker_agent)
        elapsed = timeit.default_timer() - timeit_start
        return LLMTestCase(
            name=self.name,
            input=self.input_str(),
            expected_output=self.expected_answer,
            actual_output=output,
            retrieval_context=[self.interview_context],
            completion_time=elapsed
        )


class SummariserTestCase(TestCase):
    def __init__(self, name: str, interview_context: str, conversation_history: list, question: Question, expected_answer: str, question_type: str = "open_ended", options: list = None):
        super().__init__(name)
        self.interview_context = interview_context
        self.conversation_history = conversation_history
        self.question = question if isinstance(question, Question) else Question(text=question)
        self.question_type = question_type
        self.options = options
        self.expected_answer = expected_answer

        self.retrieval_context = [f"Interview context: {self.interview_context}\nConversation history: {conversationToText(self.conversation_history)}"]
        self.input_str = f"Question: {self.question}"
        if self.question_type == "mcq" and self.options:
            options_str = "\n".join([f"{idx+1}. {option}" for idx, option in enumerate(self.options)])
            self.input_str += f"\nOptions:\n{options_str}"

    def run(self, model: Model, summariser_agent: RAG_Agent) -> str:
        """Run the test case using the provided summariser_agent."""
        summariser = summariser_agent(model, interview_context=self.interview_context)
        return summariser.answer(
            question=self.question, 
            conversation_history=self.conversation_history, 
            question_type=self.question_type, 
            options=self.options
        )

    def deepeval_testcase(self, model: Model, summariser_agent: RAG_Agent) -> LLMTestCase:
        timeit_start = timeit.default_timer()
        output = self.run(model, summariser_agent)
        elapsed = timeit.default_timer() - timeit_start
        return LLMTestCase(
            name=self.name,
            input=self.input_str,
            retrieval_context=self.retrieval_context,
            expected_output=self.expected_answer,
            actual_output=output,
            completion_time=elapsed
        )