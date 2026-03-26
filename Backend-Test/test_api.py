import sys
sys.path.append("..\\Backend")  # Add Backend directory to sys.path to allow imports

import pytest
import asyncio
import requests
from unittest.mock import Mock, patch, MagicMock
from flask import Flask
import json

from api import app, wait_for_questionnaire, send_response, start_http_server, questionnaire_data, questionnaire_received_event


@pytest.fixture
def client():
    """Create a test client for Flask app"""
    app.config['TESTING'] = True
    with app.test_client() as client:
        yield client


def test_receive_questionnaire_success(client):
    """Test successful questionnaire reception"""
    test_data = {
        "questionnaire_id": "test_123",
        "description": "Test interview",
        "questions": [
            {"text": "What is your name?", "type": "open_ended"},
            {"text": "Do you like coffee?", "type": "mcq", "options": ["Yes", "No", "Sometimes"]}
        ]
    }
    
    response = client.post('/questionnaire', json=test_data)
    
    assert response.status_code == 200
    data = json.loads(response.data)
    assert data["status"] == "success"
    assert data["message"] == "Questionnaire received"
    

def test_receive_questionnaire_no_json(client):
    """Test receiving questionnaire without JSON data"""
    response = client.post('/questionnaire', data="not json")
    
    assert response.status_code == 500
    data = json.loads(response.data)
    assert "error" in data
    assert "Unsupported Media Type" in data["error"]


def test_receive_questionnaire_missing_fields(client):
    """Test receiving questionnaire with missing required fields"""
    # Missing description
    test_data = {
        "questionnaire_id": "test_123",
        "questions": [{"text": "What is your name?", "type": "open_ended"}]
    }
    
    response = client.post('/questionnaire', json=test_data)
    assert response.status_code == 400
    data = json.loads(response.data)
    assert "Missing 'questions' or 'description'" in data["error"]


def test_receive_questionnaire_invalid_question_format(client):
    """Test receiving questionnaire with invalid question format"""
    test_data = {
        "description": "Test interview",
        "questions": [
            {"wrong_field": "What is your name?"}
        ]
    }
    
    response = client.post('/questionnaire', json=test_data)
    assert response.status_code == 400
    data = json.loads(response.data)
    assert "must be a dict with 'text' and 'type' fields" in data["error"]


def test_receive_questionnaire_empty_questions(client):
    """Test receiving questionnaire with empty questions list"""
    test_data = {
        "description": "Test interview",
        "questions": []
    }
    
    response = client.post('/questionnaire', json=test_data)
    assert response.status_code == 200
    data = json.loads(response.data)
    assert data["status"] == "success"


def test_send_response_success():
    """Test successful response sending"""
    with patch('requests.post') as mock_post:
        mock_response = MagicMock()
        mock_response.status_code = 200
        mock_post.return_value = mock_response
        
        result = send_response(
            questionnaire_id="test_123",
            question_order=1,
            question="What is your name?",
            answer="John Doe",
            port=5000,
            question_type="open_ended"
        )
        
        assert result is True
        mock_post.assert_called_once()
        
        # Verify the correct URL and payload
        call_args = mock_post.call_args
        assert call_args[0][0] == "http://localhost:5000/response"
        payload = call_args[1]["json"]
        assert payload["questionnaire_id"] == "test_123"
        assert payload["question_order"] == 1
        assert payload["question"] == "What is your name?"
        assert payload["answer"] == "John Doe"
        assert payload["question_type"] == "open_ended"
        assert payload["selected_option"] is None


def test_send_response_with_mcq():
    """Test sending response for MCQ question"""
    with patch('requests.post') as mock_post:
        mock_response = MagicMock()
        mock_response.status_code = 200
        mock_post.return_value = mock_response
        
        result = send_response(
            questionnaire_id="test_123",
            question_order=2,
            question="Do you like coffee?",
            answer="Yes",
            port=5000,
            question_type="mcq",
            selected_option="Yes"
        )
        
        assert result is True
        payload = mock_post.call_args[1]["json"]
        assert payload["selected_option"] == "Yes"
        assert payload["question_type"] == "mcq"


def test_send_response_connection_error():
    """Test sending response with connection error"""
    with patch('requests.post') as mock_post:
        mock_post.side_effect = requests.exceptions.ConnectionError("Connection refused")
        
        result = send_response(
            questionnaire_id="test_123",
            question_order=1,
            question="Test",
            answer="Answer",
            port=5000,
            question_type="open_ended"
        )
        
        assert result is False


def test_send_response_timeout():
    """Test sending response with timeout"""
    with patch('requests.post') as mock_post:
        mock_post.side_effect = requests.exceptions.Timeout("Request timeout")
        
        result = send_response(
            questionnaire_id="test_123",
            question_order=1,
            question="Test",
            answer="Answer",
            port=5000,
            question_type="open_ended"
        )
        
        assert result is False


def test_send_response_http_error():
    """Test sending response with HTTP error status"""
    with patch('requests.post') as mock_post:
        mock_response = MagicMock()
        mock_response.status_code = 500
        mock_response.text = "Internal Server Error"
        mock_post.return_value = mock_response
        
        result = send_response(
            questionnaire_id="test_123",
            question_order=1,
            question="Test",
            answer="Answer",
            port=5000,
            question_type="open_ended"
        )
        
        assert result is False


def test_questionnaire_global_reset():
    """Test that global variables reset properly between tests"""
    # This test ensures we clean up global state
    global questionnaire_data, questionnaire_received_event
    questionnaire_data = None
    questionnaire_received_event.clear()
    
    assert questionnaire_data is None
    assert not questionnaire_received_event.is_set()