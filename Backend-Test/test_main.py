import sys
sys.path.append("..\\Backend")  # Add Backend directory to sys.path to allow imports

import pytest
from unittest.mock import Mock, patch, AsyncMock
from main import AvatarFormsInterviewer, main
from agents import TalkerAgent, EvaluatorAgent, RAG_Agent
import asyncio

class TestAvatarFormsInterviewer:
    def setup_method(self):
        """Setup before each test"""
        self.interviewer = AvatarFormsInterviewer(is_local=True, local_port=8081, cutoff=4)

    def test_initialization(self):
        """Test initialization of AvatarFormsInterviewer"""
        assert self.interviewer.cutoff == 4
        assert self.interviewer.is_local is True
        assert self.interviewer.local_port == 8081
        assert self.interviewer.user_role == "user"
        assert self.interviewer.AI_role == "assistant"

        assert self.interviewer.tracer is None

    @patch('agents.Model')
    def test_get_model_local(self, mock_model):
        """Test getting local model"""

        interviewer = AvatarFormsInterviewer(is_local=True, local_port=8081)
        result = interviewer.get_model()
        assert result.url == "http://127.0.0.1:8081/v1/chat/completions"

    @patch('agents.Model')
    @patch('os.getenv')
    def test_get_model_fireworks(self, mock_getenv, mock_model):
        """Test getting Fireworks model"""
        mock_getenv.return_value = "test-api-key"
        interviewer = AvatarFormsInterviewer(is_local=False, model_name="test-model")
        result = interviewer.get_model()
        assert result.url == "https://api.fireworks.ai/inference/v1/chat/completions"
        assert result.model == "test-model"
        assert result.api_key == "test-api-key"

    @patch('os.getenv')
    def test_get_model_fireworks_no_key(self, mock_getenv):
        """Test Fireworks model with no API key"""
        mock_getenv.return_value = None
        interviewer = AvatarFormsInterviewer(is_local=False)
        with pytest.raises(ValueError) as exc_info:
            interviewer.get_model()
        assert "FIREWORKS_API_KEY" in str(exc_info.value)

    def test_build_interview(self):
        """Test building interview"""
        questions = ["Q1", "Q2"]
        context = "Test context"
        
        with patch.object(self.interviewer, 'get_model') as mock_get_model:
            mock_get_model.return_value = Mock()
            self.interviewer.build_interview(questions, context)
            
        assert len(self.interviewer.questions) == 2
        assert self.interviewer.interview_context == context
        assert self.interviewer.questions_index == 0
        assert self.interviewer.conversation_history == []
        assert self.interviewer.question_labels == []
        assert self.interviewer.answers == ["", ""]

    def test_build_from_json(self):
        """Test building from JSON"""
        json_data = {
            "questions": [
                {"text": "Q1", "type": "open_ended"},
                {"text": "Q2", "type": "mcq", "options": ["A", "B"]}
            ],
            "description": "Test"
        }
        
        with patch.object(self.interviewer, 'get_model') as mock_get_model:
            mock_get_model.return_value = Mock()
            self.interviewer.build_from_json(json_data)
            
        assert len(self.interviewer.questions) == 2
        assert self.interviewer.questions[0]["text"] == "Q1"
        assert self.interviewer.questions[1]["text"] == "Q2"
        assert self.interviewer.questions[0]["type"] == "open_ended"
        assert self.interviewer.questions[1]["type"] == "mcq"
        assert self.interviewer.questions[1]["options"] == ["A", "B"]
        assert self.interviewer.interview_context == "Test"

    def test_reset_interview(self):
        """Test resetting interview"""
        self.interviewer.questions = [{"text": "Q1"}, {"text": "Q2"}]
        self.interviewer.questions_index = 2
        self.interviewer.conversation_history = [{"role": "user", "content": "test"}]
        self.interviewer.question_labels = [0, 1, 1]
        self.interviewer.last_evaluation = {"test": "data"}
        
        self.interviewer.reset_interview()
        
        assert self.interviewer.questions_index == 0
        assert self.interviewer.conversation_history == []
        assert self.interviewer.question_labels == []
        assert self.interviewer.last_evaluation is None

    def test_get_conversation_section(self):
        """Test getting conversation section for a question"""
        self.interviewer.conversation_history = [
            {"role": "assistant", "content": "Q1"},
            {"role": "user", "content": "A1"},
            {"role": "assistant", "content": "Q2"},
            {"role": "user", "content": "A2"}
        ]
        self.interviewer.question_labels = [0, 0, 1, 1]
        
        section = self.interviewer.get_conversation_section(0)
        assert len(section) == 2
        assert section[0]["content"] == "Q1"
        assert section[1]["content"] == "A1"

    def test_should_cutoff(self):
        """Test cutoff condition"""
        self.interviewer.questions_index = 0
        self.interviewer.cutoff = 2
        
        # Less than cutoff
        self.interviewer.conversation_history = [{"role": "assistant"}, {"role": "user"}]
        self.interviewer.question_labels = [0, 0]
        assert self.interviewer.should_cutoff() is False
        
        # At cutoff
        self.interviewer.conversation_history = [{"role": "assistant"}, {"role": "user"}, 
                                                {"role": "assistant"}, {"role": "user"}]
        self.interviewer.question_labels = [0, 0, 0, 0]
        assert self.interviewer.should_cutoff() is True

    @patch.object(TalkerAgent, 'ask_question')
    def test_start_interview(self, mock_ask_question):
        """Test starting interview"""
        mock_ask_question.return_value = "What is your name?"
        
        self.interviewer.questions = [{"text": "What is your name?", "type": "open_ended"}]
        self.interviewer.questions_index = 0
        self.interviewer.interview_context = "Test context"
        self.interviewer.conversation_history = []
        self.interviewer.question_labels = []

        self.interviewer.talker = Mock()
        self.interviewer.talker.ask_question = mock_ask_question
        
        result = self.interviewer.start_interview()
        
        assert result == "What is your name?"
        assert len(self.interviewer.conversation_history) == 1
        assert self.interviewer.conversation_history[0]["role"] == "assistant"
        assert self.interviewer.conversation_history[0]["content"] == "What is your name?"
        assert self.interviewer.question_labels == [0]

    @patch.object(EvaluatorAgent, 'evaluate')
    @patch.object(TalkerAgent, 'ask_question')
    @patch.object(TalkerAgent, 'ask_followup')
    def test_respond_followup(self, mock_followup, mock_ask, mock_evaluate):
        """Test responding and continuing interview"""
        # Setup
        self.interviewer.questions = [{"text": "Q1", "type": "open_ended"}, {"text": "Q2", "type": "mcq", "options": ["A", "B"]}]
        self.interviewer.questions_index = 0
        self.interviewer.conversation_history = [{"role": "assistant", "content": "Q1"}]
        self.interviewer.question_labels = [0]
        self.interviewer.interview_context = "Test context"
        self.interviewer.last_evaluation = None
        self.interviewer.answers = ["", ""]

        self.interviewer.talker = Mock()
        self.interviewer.talker.ask_question = mock_ask
        self.interviewer.talker.ask_followup = mock_followup
        self.interviewer.evaluator = Mock()
        self.interviewer.evaluator.evaluate = mock_evaluate
        
        mock_evaluate.return_value = {
            "satisfactory": False,
            "override_skip": False,
            "reasoning": "Test",
            "follow_up_question": "Follow up?"
        }
        mock_followup.return_value = "Follow up question"
        
        # Execute
        result, should_continue = self.interviewer.respond("User response")
        
        # Assert
        assert should_continue is True
        assert len(self.interviewer.conversation_history) == 3  # Original question + user response + follow-up
        assert self.interviewer.question_labels == [0, 0, 0]
        assert self.interviewer.last_evaluation["satisfactory"] is False
        assert self.interviewer.last_evaluation["follow_up_question"] == "Follow up?"
        assert result == "Follow up question"
        assert self.interviewer.question_labels == [0, 0, 0]

    @patch.object(EvaluatorAgent, 'evaluate')
    @patch.object(TalkerAgent, 'closing_statement')
    @patch.object(RAG_Agent, 'answer')
    def test_respond_end_interview(self, mock_closing, mock_evaluate, mock_answer):
        """Test responding and ending interview"""
        # Setup
        self.interviewer.questions = [{"text": "Q1", "type": "open_ended"}]
        self.interviewer.questions_index = 0
        self.interviewer.conversation_history = [{"role": "assistant", "content": "Q1"}]
        self.interviewer.question_labels = [0]
        self.interviewer.interview_context = "Test context"
        self.interviewer.last_evaluation = None
        self.interviewer.answers = [""]

        self.interviewer.talker = Mock()
        self.interviewer.talker.closing_statement = mock_closing
        self.interviewer.evaluator = Mock()
        self.interviewer.evaluator.evaluate = mock_evaluate
        self.interviewer.rag_agent = Mock()
        self.interviewer.rag_agent.answer = mock_answer
        
        mock_evaluate.return_value = {
            "satisfactory": True,
            "override_skip": False,
            "reasoning": "Good answer",
            "follow_up_question": None
        }
        mock_closing.return_value = "Thank you!"
        mock_answer.return_value = "Answer"
        
        # Execute
        result, should_continue = self.interviewer.respond("User response")
        
        # Assert
        assert should_continue is False
        assert result == "Thank you!"
        assert len(self.interviewer.conversation_history) == 3 # Original question + user response + closing statement
        assert self.interviewer.question_labels == [0, 0, 1]
        assert self.interviewer.last_evaluation is None # Should reset evaluation after ending question
        assert self.interviewer.answers[0] == "Answer"

    @patch.object(EvaluatorAgent, 'evaluate')
    @patch.object(TalkerAgent, 'ask_question')
    @patch.object(RAG_Agent, 'answer')
    def test_respond_skip_question(self, mock_evaluate, mock_ask, mock_answer):
        """Test responding and skipping question"""
        # Setup
        self.interviewer.questions = [{"text": "Q1", "type": "open_ended"}, {"text": "Q2", "type": "mcq", "options": ["A", "B"]}]
        self.interviewer.questions_index = 0
        self.interviewer.conversation_history = [{"role": "assistant", "content": "Q1"}]
        self.interviewer.question_labels = [0]
        self.interviewer.interview_context = "Test context"
        self.interviewer.last_evaluation = None
        self.interviewer.answers = ["", ""]

        self.interviewer.talker = Mock()
        self.interviewer.talker.ask_question = mock_ask
        self.interviewer.evaluator = Mock()
        self.interviewer.evaluator.evaluate = mock_evaluate
        self.interviewer.rag_agent = Mock()
        self.interviewer.rag_agent.answer = mock_answer

        mock_ask.return_value = "Q2"
        mock_evaluate.return_value = {
            "satisfactory": False,
            "override_skip": True,
            "reasoning": "User wants to skip",
            "follow_up_question": None
        }
        
        # Execute
        result, should_continue = self.interviewer.respond("User response")
        
        # Assert
        assert should_continue is True
        assert len(self.interviewer.conversation_history) == 4 # Original question + user response + skip message + next question
        assert self.interviewer.question_labels == [0, 0, 0, 1]
        assert self.interviewer.last_evaluation is None # Should reset evaluation after moving to new question
        assert result == "Q2"


    @patch.object(RAG_Agent, 'answer')
    def test_collect_answer_open_ended(self, mock_answer):
        """Test collecting answer for open-ended question"""
        self.interviewer.questions = [{"text": "Q1", "type": "open_ended"}]
        self.interviewer.answers = [""]
        self.interviewer.rag_agent = Mock()
        self.interviewer.rag_agent.answer = mock_answer
        mock_answer.return_value = "John Doe"
        
        # Add conversation history
        self.interviewer.conversation_history = [
            {"role": "assistant", "content": "What's your name?"},
            {"role": "user", "content": "My name is John Doe"}
        ]
        self.interviewer.question_labels = [0, 0]
        
        result = self.interviewer.collect_answer(0)
        
        assert result == "John Doe"
        mock_answer.assert_called_once()

    @patch.object(RAG_Agent, 'answer')
    @patch('main.match_mcq_option')
    def test_collect_answer_mcq(self, mock_match, mock_answer):
        """Test collecting answer for MCQ"""
        self.interviewer.questions = [
            {"text": "Do you like coffee?", "type": "mcq", "options": ["Yes", "No", "Maybe"]}
        ]
        self.interviewer.rag_agent = Mock()
        self.interviewer.rag_agent.answer = mock_answer
        mock_answer.return_value = "yes"
        mock_match.return_value = "Yes"
        
        # Add conversation history
        self.interviewer.conversation_history = [
            {"role": "assistant", "content": "Do you like coffee?"},
            {"role": "user", "content": "yes"}
        ]
        self.interviewer.question_labels = [0, 0]
        
        result = self.interviewer.collect_answer(0)
        
        assert result == "Yes"
        mock_match.assert_called_once_with("yes", ["Yes", "No", "Maybe"])

    def test_collect_all_answers(self):
        """Test collecting all answers"""
        self.interviewer.questions = [
            {"text": "Q1", "type": "open_ended"},
            {"text": "Q2", "type": "mcq", "options": ["A", "B"]}
        ]
        self.interviewer.answers = ["Answer 1", "Answer 2"]
        
        result = self.interviewer.collect_all_answers()
        
        assert result == {
            "Q1": "Answer 1",
            "Q2": "Answer 2"
        }

    @patch('csv.writer')
    def test_output_to_csv(self, mock_csv_writer):
        """Test outputting to CSV"""
        mock_file = Mock()
        mock_writer = Mock()
        mock_csv_writer.return_value = mock_writer
        
        final_answers = {
            "Question 1": "Answer 1",
            "Question 2": "Answer 2"
        }
        
        with patch('builtins.open', create=True) as mock_open:
            mock_open.return_value.__enter__.return_value = mock_file
            self.interviewer.output_to_csv("test.csv", final_answers)
            
            # Check that writer was called with correct data
            assert mock_writer.writerow.call_count == 3  # Header + 2 rows
            mock_writer.writerow.assert_any_call(["Question", "Answer"])
            mock_writer.writerow.assert_any_call(["Question 1", "Answer 1"])
            mock_writer.writerow.assert_any_call(["Question 2", "Answer 2"])

    @patch('csv.writer')
    def test_output_to_csv_empty_answers(self, mock_csv_writer):
        """Test outputting to CSV with empty answers"""
        mock_file = Mock()
        mock_writer = Mock()
        mock_csv_writer.return_value = mock_writer
        
        final_answers = {}
        
        with patch('builtins.open', create=True) as mock_open:
            mock_open.return_value.__enter__.return_value = mock_file
            self.interviewer.output_to_csv("test.csv", final_answers)
            
            # Check that only header was written
            assert mock_writer.writerow.call_count == 1
            mock_writer.writerow.assert_called_with(["Question", "Answer"])
    

@pytest.mark.asyncio
async def test_main_function():
    """Test the main() function with mocked dependencies"""
    
    # Mock questionnaire data
    mock_questionnaire = {
        "questionnaire_id": "test_id_123",
        "description": "Test interview",
        "questions": [
            {"text": "What is your name?", "type": "open_ended"},
            {"text": "How are you feeling?", "type": "open_ended"}
        ]
    }
    
    mock_input_responses = ["John Doe", "I'm doing well, thank you!"]
    async def mock_to_thread(func, *args, **kwargs):
        return func(*args, **kwargs)
    
    # Patch all external dependencies
    with patch('main.start_http_server') as mock_start_http, \
         patch('main.wait_for_questionnaire', new_callable=AsyncMock) as mock_wait_questionnaire, \
         patch('main.start_server', new_callable=AsyncMock) as mock_start_server, \
         patch('main.wait_for_browser_connection', new_callable=AsyncMock) as mock_wait_browser, \
         patch('main.stream_message', new_callable=AsyncMock) as mock_stream, \
         patch('main.send_response') as mock_send_response, \
         patch('builtins.input', side_effect=mock_input_responses), \
         patch('asyncio.to_thread', side_effect=mock_to_thread):
        
        # Configure mocks
        mock_start_server.return_value = AsyncMock()
        mock_wait_questionnaire.return_value = mock_questionnaire
        
        # Create a mock server object
        mock_server = AsyncMock()
        mock_start_server.return_value = mock_server
        
        # Run the main function with test arguments
        with patch('sys.argv', ['main.py', '--local', '--port', '8883', '--http_port', '8882']):
            try:
                with patch('main.AvatarFormsInterviewer') as MockInterviewer:
                    # Setup mock interviewer
                    mock_interviewer_instance = Mock()
                    mock_interviewer_instance.start_interview.return_value = "What is your name?"
                    mock_interviewer_instance.respond.side_effect = [
                        ("Nice to meet you John!", True),
                        ("Thank you for sharing!", False)
                    ]
                    mock_interviewer_instance.collect_all_answers.return_value = {
                        "What is your name?": "John Doe",
                        "How are you?": "I'm doing well, thank you!"
                    }
                    mock_interviewer_instance.questions = mock_questionnaire["questions"]
                    MockInterviewer.return_value = mock_interviewer_instance
                    
                    task = asyncio.create_task(main())
                    
                    # Wait for a short time to let it run
                    await asyncio.sleep(0.5)
                    
                    # Cancel the task if it's still running
                    if not task.done():
                        task.cancel()
                        try:
                            await task
                        except asyncio.CancelledError:
                            pass
                    
                    # Assert that mocked functions were called
                    mock_start_http.assert_called_once()
                    mock_wait_questionnaire.assert_called_once()
                    mock_start_server.assert_called_once()
                    mock_wait_browser.assert_called_once()
                    
                    # Verify that send_response was called for each question
                    assert mock_send_response.call_count == 2
                    assert mock_send_response.call_args_list[0].kwargs['answer'] == "John Doe"
                    assert mock_send_response.call_args_list[1].kwargs['answer'] == "I'm doing well, thank you!"
                    
            except asyncio.TimeoutError:
                pytest.fail("Main function timed out")