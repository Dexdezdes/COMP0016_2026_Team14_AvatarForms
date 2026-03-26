import sys
sys.path.append("..\\Backend")  # Add Backend directory to sys.path to allow imports

from tracing import Tracer
import pytest
from unittest.mock import Mock, patch
import json
from agents import Model, Agent, TalkerAgent, EvaluatorAgent, RAG_Agent

class TestModel:
    def test_model_initialization_with_default_params(self):
        """Test Model initialization with default parameters"""
        model = Model(url="http://test.com", model="test-model")
        assert model.url == "http://test.com"
        assert model.model == "test-model"
        assert model.api_key is None
        assert "max_tokens" in model.params
        assert model.params["temperature"] == 0.6

    def test_model_initialization_with_custom_params(self):
        """Test Model initialization with custom parameters"""
        custom_params = {"temperature": 0.8, "max_tokens": 1000}
        model = Model(url="http://test.com", model="test-model", 
                     api_key="test-key", params=custom_params)
        assert model.api_key == "test-key"
        assert model.params["temperature"] == 0.8
        assert model.params["max_tokens"] == 1000

    @patch('requests.request')
    def test_generate_success(self, mock_request):
        """Test successful model generation"""
        mock_response = Mock()
        mock_response.status_code = 200
        mock_response.json.return_value = {"choices": [{"message": {"content": "Test response"}}]}
        mock_request.return_value = mock_response

        model = Model(url="http://test.com", model="test-model")
        messages = [{"role": "user", "content": "Hello"}]
        
        result = model.generate(messages, temperature=0.3)
        
        assert result.json() == mock_response.json()
        mock_request.assert_called_once()

    @patch('requests.request')
    def test_generate_with_api_key(self, mock_request):
        """Test generation with API key authorization"""
        mock_response = Mock()
        mock_response.status_code = 200
        mock_response.json.return_value = {"choices": [{"message": {"content": "Test"}}]}
        mock_request.return_value = mock_response

        model = Model(url="http://test.com", model="test-model", api_key="secret-key")
        messages = [{"role": "user", "content": "Hello"}]
        
        model.generate(messages)
        
        # Check if authorization header was included
        call_args = mock_request.call_args[1]
        assert call_args['headers']['Authorization'] == "Bearer secret-key"

    @patch('requests.request')
    def test_generate_failure(self, mock_request):
        """Test failed model generation"""
        mock_response = Mock()
        mock_response.status_code = 500
        mock_response.text = "Internal Server Error"
        mock_request.return_value = mock_response

        model = Model(url="http://test.com", model="test-model")
        messages = [{"role": "user", "content": "Hello"}]
        
        with pytest.raises(Exception) as exc_info:
            model.generate(messages)
        
        assert "API call failed" in str(exc_info.value)

class TestAgent:
    def test_agent_initialization(self):
        """Test Agent initialization"""
        mock_model = Mock(spec=Model)
        agent = Agent(mock_model)
        assert agent.model == mock_model

    @patch.object(Model, 'generate')
    @patch.object(Tracer, 'log')
    def test_agent_run(self, mock_generate, mock_tracer):
        """Test Agent run method"""
        mock_response = Mock(json=lambda: {"choices": [{"message": {"content": "Test output"}}]}, elapsed=Mock(total_seconds=lambda: 2.5))
        mock_generate.return_value = mock_response

        mock_model = Mock(spec=Model)
        mock_model.generate = mock_generate
        agent = Agent(mock_model, tracer=mock_tracer)
        
        messages = [{"role": "user", "content": "Hello"}]
        result = agent.run(messages)
        
        assert result == "Test output"

        mock_generate.assert_called_once_with(messages, temperature=None)
        mock_tracer.log.assert_called_once()


class TestTalkerAgent:
    def setup_method(self):
        self.mock_model = Mock(spec=Model)
        self.mock_tracer = Mock(spec=Tracer)
        self.conversation_history = []
        self.interview_context = "Test interview context"
        self.talker = TalkerAgent(self.mock_model, self.conversation_history, self.interview_context, tracer=self.mock_tracer)

    @patch.object(Agent, 'run')
    def test_ask_question(self, mock_run):
        """Test asking a question"""
        mock_run.return_value = "Hello, what is your name?"
        
        result = self.talker.ask_question("What is your name?")
        
        assert result == "Hello, what is your name?"
        mock_run.assert_called_once()

    @patch.object(Agent, 'run')
    @patch('agents.format_q_and_as')
    def test_ask_question_with_history(self, mock_format_q_and_as, mock_run):
        """Test asking a question with conversation history"""
        self.conversation_history.append({"role": "assistant", "content": "Previous message"})
        mock_run.return_value = "What is your age?"
        mock_format_q_and_as.return_value = "Q: What is your name?\nA: John"

        result = self.talker.ask_question("What is your age?", previous_q_and_a={"What is your name?": "John"})
        
        assert result == "What is your age?"
        mock_run.assert_called_once()
        mock_format_q_and_as.assert_called_once()

    @patch.object(Agent, 'run')
    @patch('agents.format_q_and_as')
    def test_ask_followup(self, mock_format_q_and_as, mock_run):
        """Test asking a follow-up question"""
        mock_run.return_value = "Can you tell me your last name?"
        mock_format_q_and_as.return_value = "Q: What is your name?\nA: John"
        
        result = self.talker.ask_followup(
            question="What is your name?",
            reasoning="Answer too brief",
            transcript=[{"role": "user", "content": "John"}],
            previous_q_and_a={"What is your name?": "John"},
            follow_up="Please tell me your last name as well."
        )
        
        assert result == "Can you tell me your last name?"
        mock_run.assert_called_once()
        mock_format_q_and_as.assert_called_once()

    @patch.object(Agent, 'run')
    def test_closing_statement(self, mock_run):
        """Test closing statement generation"""
        mock_run.return_value = "Thank you for your time!"
        
        result = self.talker.closing_statement()
        
        assert result == "Thank you for your time!"
        mock_run.assert_called_once()

class TestEvaluatorAgent:
    def setup_method(self):
        self.mock_model = Mock(spec=Model)
        self.mock_tracer = Mock(spec=Tracer)
        self.interview_context = "Test context"
        self.evaluator = EvaluatorAgent(self.mock_model, self.interview_context, tracer=self.mock_tracer)

    @patch.object(Agent, 'run')
    def test_evaluate_satisfactory(self, mock_run):
        """Test evaluation with satisfactory answer"""
        mock_run.return_value = json.dumps({
            "satisfactory": True,
            "override_skip": False,
            "reasoning": "Complete answer",
            "follow_up_question": None
        })
        
        result = self.evaluator.evaluate(
            question="What is your name?",
            conversation_history=[{"role": "user", "content": "My name is John"}]
        )
        
        assert result["satisfactory"] is True
        assert result["override_skip"] is False
        assert result["follow_up_question"] is None

    @patch.object(Agent, 'run')
    def test_evaluate_unsatisfactory(self, mock_run):
        """Test evaluation with unsatisfactory answer"""
        mock_run.return_value = json.dumps({
            "satisfactory": False,
            "override_skip": False,
            "reasoning": "Incomplete answer",
            "follow_up_question": "Can you provide more details?"
        })
        
        result = self.evaluator.evaluate(
            question="What is your name?",
            conversation_history=[{"role": "user", "content": "John"}]
        )
        
        assert result["satisfactory"] is False
        assert result["follow_up_question"] == "Can you provide more details?"

class TestRAGAgent:
    def setup_method(self):
        self.mock_model = Mock(spec=Model)
        self.interview_context = "Test context"
        self.mock_tracer = Mock(spec=Tracer)
        self.rag_agent = RAG_Agent(self.mock_model, self.interview_context, tracer=self.mock_tracer)

    @patch.object(Agent, 'run')
    def test_answer_open_ended(self, mock_run):
        """Test answering an open-ended question"""
        mock_run.return_value = "John Doe"
        
        result = self.rag_agent.answer(
            question="What is your name?",
            conversation_history=[{"role": "user", "content": "My name is John Doe"}]
        )
        
        assert result == "John Doe"
        mock_run.assert_called_once()

    @patch.object(Agent, 'run')
    def test_answer_mcq(self, mock_run):
        """Test answering an MCQ question"""
        mock_run.return_value = "Yes"
        
        result = self.rag_agent.answer(
            question="Do you like coffee?",
            conversation_history=[{"role": "user", "content": "I love coffee, so yes"}],
            question_type="mcq",
            options=["Yes", "No", "Maybe"]
        )
        
        assert result == "Yes"
        mock_run.assert_called_once()