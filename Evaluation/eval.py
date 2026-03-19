import sys
sys.path.append("..\\Backend")  # Add parent directory to sys.path to allow imports from Backend

from agents import Agent, TalkerAgent, EvaluatorAgent, RAG_Agent, Model
from main import AvatarFormsInterviewer
from formatting import conversationToText
from test_cases import talker_test_cases, evaluator_test_cases, summariser_test_cases
from test_case import TalkerTestCase, EvaluatorTestCase, SummariserTestCase

from deepeval.test_case import LLMTestCase, LLMTestCaseParams
from deepeval.metrics import AnswerRelevancyMetric, FaithfulnessMetric, GEval
from deepeval import evaluate
from deepeval.models import GPTModel

class AgentEvaluation:
    def __init__(self, judge_model: str, agent_model: Model, talker_agent: Agent, evaluator_agent: Agent, summariser_agent: Agent):
        self.judge_model = GPTModel(model=judge_model)
        self.agent_model = agent_model
        self.talker_agent = talker_agent
        self.evaluator_agent = evaluator_agent
        self.summariser_agent = summariser_agent

    def run_summariser_tests(self, test_cases: list[SummariserTestCase]):
        deepeval_test_cases = [test_case.deepeval_testcase(model=self.agent_model, summariser_agent=self.summariser_agent) for test_case in test_cases]
        
        metrics = [
            AnswerRelevancyMetric(model=self.judge_model),
            FaithfulnessMetric(model=self.judge_model)
        ]
        evaluate(deepeval_test_cases, metrics)
    
    def run_evaluator_tests(self, test_cases: list[EvaluatorTestCase]):
        deepeval_test_cases = [test_case.deepeval_testcase(model=self.agent_model, evaluator_agent=self.evaluator_agent) for test_case in test_cases]
        
        # Define metrics for Evaluator evaluation
        metrics = [
            GEval(
                name="Satisfactory Judgment Accuracy",
                model=self.judge_model,
                criteria="Determine if the evaluator correctly identified whether the answer was satisfactory.",
                evaluation_params=[LLMTestCaseParams.ACTUAL_OUTPUT, LLMTestCaseParams.EXPECTED_OUTPUT],
                threshold=0.8
            ),
            GEval(
                name="Skip Decision Accuracy",
                model=self.judge_model,
                criteria="Determine if the evaluator correctly identified when to skip a question.",
                evaluation_params=[LLMTestCaseParams.ACTUAL_OUTPUT, LLMTestCaseParams.EXPECTED_OUTPUT],
                threshold=0.8
            )
        ]
        evaluate(deepeval_test_cases, metrics)

    def run_talker_tests(self, test_cases: list[TalkerTestCase]):
        deepeval_test_cases = [test_case.deepeval_testcase(model=self.agent_model, talker_agent=self.talker_agent) for test_case in test_cases]
                # Define metrics for Talker evaluation
        metrics = [
            AnswerRelevancyMetric(model=self.judge_model, threshold=0.7),
            GEval(
                name="Question Appropriateness",
                model=self.judge_model,
                criteria="Determine if the question is asked appropriately, clearly, and matches the expected format.",
                evaluation_params=[LLMTestCaseParams.ACTUAL_OUTPUT, LLMTestCaseParams.EXPECTED_OUTPUT],
                threshold=0.7
            )
        ]
        evaluate(deepeval_test_cases, metrics)


if __name__ == "__main__":

    interviewer = AvatarFormsInterviewer(is_local=False, model_name="accounts/fireworks/models/qwen3-8b")
    model = interviewer.get_model()
    agent_evaluation = AgentEvaluation(
        judge_model="gpt-5-mini",
        agent_model=model,
        talker_agent=TalkerAgent,
        evaluator_agent=EvaluatorAgent,
        summariser_agent=RAG_Agent
    )

    # Run evaluations and capture results
    summariser_results = agent_evaluation.run_summariser_tests(summariser_test_cases)
    print(summariser_results)
    
    evaluator_results = agent_evaluation.run_evaluator_tests(evaluator_test_cases)
    print(evaluator_results)
    
    talker_results = agent_evaluation.run_talker_tests(talker_test_cases)
    print(talker_results)