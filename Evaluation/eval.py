import argparse
import sys
sys.path.append("..\\Backend")  # Add Backend directory to sys.path to allow imports

from tracing import Tracer
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
    def __init__(self, judge_model: str, agent_model: Model, talker_agent: Agent, evaluator_agent: Agent, summariser_agent: Agent, tracer: Tracer = None) -> None:
        self.judge_model = GPTModel(model=judge_model)
        self.agent_model = agent_model
        self.talker_agent = talker_agent
        self.evaluator_agent = evaluator_agent
        self.summariser_agent = summariser_agent
        self.tracer = tracer

    def run_summariser_tests(self, test_cases: list[SummariserTestCase]) -> None:
        deepeval_test_cases = [test_case.deepeval_testcase(model=self.agent_model, summariser_agent=self.summariser_agent, tracer=self.tracer) for test_case in test_cases]
        
        metrics = [
            AnswerRelevancyMetric(model=self.judge_model),
            GEval(
                name="Faithfulness",
                model=self.judge_model,
                criteria="Check that the output reflects the user’s answer and relevant information without contradicting it, allowing for paraphrasing, simplification, and minor omissions.",
                evaluation_params=[LLMTestCaseParams.ACTUAL_OUTPUT, LLMTestCaseParams.EXPECTED_OUTPUT],
                threshold=0.5
            )
        ]
        evaluate(deepeval_test_cases, metrics)
    
    def run_evaluator_tests(self, test_cases: list[EvaluatorTestCase]) -> None:
        deepeval_test_cases = [test_case.deepeval_testcase(model=self.agent_model, evaluator_agent=self.evaluator_agent, tracer=self.tracer) for test_case in test_cases]
        
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

    def run_talker_tests(self, test_cases: list[TalkerTestCase]) -> None:
        deepeval_test_cases = [test_case.deepeval_testcase(model=self.agent_model, talker_agent=self.talker_agent, tracer=self.tracer) for test_case in test_cases]
                # Define metrics for Talker evaluation
        metrics = [
            AnswerRelevancyMetric(model=self.judge_model, threshold=0.7),
            GEval(
                name="Question Appropriateness",
                model=self.judge_model,
                criteria="Assess whether the question is generally understandable, reasonably appropriate for the context, and loosely follows the expected format. Minor differences in phrasing, tone, or structure should be accepted as long as the intent is clear.",
                evaluation_params=[LLMTestCaseParams.ACTUAL_OUTPUT, LLMTestCaseParams.EXPECTED_OUTPUT],
                threshold=0.7
            )
        ]
        evaluate(deepeval_test_cases, metrics)


if __name__ == "__main__":

    parser = argparse.ArgumentParser(description="Run DeepEval evals for the AvatarForms agent.")
    parser.add_argument("--port", type=int, default=8081, help="Port for local model server if --local is set (default: 8081)")

    parser.add_argument("--talker", action="store_true", help="Run tests for the talker agent")
    parser.add_argument("--evaluator", action="store_true", help="Run tests for the evaluator agent")
    parser.add_argument("--summariser", action="store_true", help="Run tests for the summariser agent")


    args = parser.parse_args()



    interviewer = AvatarFormsInterviewer(is_local=True, local_port=args.port)
    model = interviewer.get_model()
    agent_evaluation = AgentEvaluation(
        judge_model="gpt-5-mini",
        agent_model=model,
        talker_agent=TalkerAgent,
        evaluator_agent=EvaluatorAgent,
        summariser_agent=RAG_Agent,
        tracer=Tracer(log_dir="agent_evaluation_logs", print_logs=False)
    )

    # Run evaluations
    # Results save to folder specified in .env file (DEEPEVAL_RESULTS_FOLDER)
    if not (args.talker or args.evaluator or args.summariser):
        args.talker = args.evaluator = args.summariser = True
    
    if args.summariser:
        agent_evaluation.run_summariser_tests(summariser_test_cases)
    if args.evaluator:
        agent_evaluation.run_evaluator_tests(evaluator_test_cases)
    if args.talker:
        agent_evaluation.run_talker_tests(talker_test_cases)
