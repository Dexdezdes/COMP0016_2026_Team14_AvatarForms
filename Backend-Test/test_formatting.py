import sys
sys.path.append("..\\Backend")  # Add Backend directory to sys.path to allow imports

import pytest
import json
from formatting import (
    clean_script, emojiStrip, LLM_strip, bracketStrip, tagStrip,
    outputToJSON, conversationToText, format_q_and_as, bcolors,
    format_question, match_mcq_option
)
from agents import Question

class TestCleanScript:
    def test_clean_script_removes_tags_and_emojis(self):
        """Test that clean_script removes LLM tags, brackets, and emojis"""
        text = "<thinking>Goodbye World</thinking> [world] {test} 😊 Hello World"
        result = clean_script(text)
        assert "Hello World" in result
        assert "😊" not in result
        assert "<thinking>" not in result
        assert "</thinking>" not in result
        assert "[world]" not in result
        assert "{test}" not in result
        assert "Goodbye World" not in result

class TestEmojiStrip:
    def test_emoji_strip_removes_common_emojis(self):
        """Test removal of common emojis"""
        text = "Hello 😊 world 🌍 test 😂"
        result = emojiStrip(text)
        assert "Hello" in result
        assert "world" in result
        assert "test" in result
        assert "😊" not in result
        assert "🌍" not in result
        assert "😂" not in result

    def test_emoji_strip_preserves_text(self):
        """Test that regular text is preserved"""
        text = "Hello, world! This is a test.£$%^&*()"
        result = emojiStrip(text)
        assert result == "Hello, world! This is a test.£$%^&*()"

class TestLLMStrip:
    def test_llm_strip_removes_xml_tags(self):
        """Test removal of XML-like tags"""
        text = "<thinking>Some thought</thinking> Actual content<more>Extra</more>"
        result = LLM_strip(text)
        assert result == "Actual content"

    def test_llm_strip_handles_nested_tags(self):
        """Test handling of nested tags"""
        text = "<outer><inner>Content</inner></outer> Outside"
        result = LLM_strip(text)
        assert result == "Outside"

class TestBracketStrip:
    def test_bracket_strip_removes_all_brackets(self):
        """Test removal of content in all bracket types"""
        text = "Hello (world) [test] {here} outside"
        result = bracketStrip(text)
        assert result == "Hello    outside"

    def test_bracket_strip_with_nested_brackets(self):
        """Test handling of nested brackets"""
        text = "Start [inside (nested)] end"
        result = bracketStrip(text)
        assert result == "Start  end"

class TestTagStrip:
    def test_tag_strip_removes_specific_tags(self):
        """Test removal of content between specific tags"""
        text = "Hello &world£ test &here£ end"
        result = tagStrip(text, "&", "£")
        assert result == "Hello  test  end"

    def test_tag_strip_with_no_tags(self):
        """Test when no tags are present"""
        text = "Hello world"
        result = tagStrip(text, "[", "]")
        assert result == "Hello world"

class TestOutputToJSON:
    def test_output_to_json_valid(self):
        """Test parsing valid JSON"""
        text = '{"key": "value", "number": 123}'
        result = outputToJSON(text)
        assert result == {"key": "value", "number": 123}

    def test_output_to_json_with_llm_tags(self):
        """Test parsing JSON with LLM tags"""
        text = '<thinking>Some thought</thinking>{"key": "value", "number": 123}'
        result = outputToJSON(text)
        assert result == {"key": "value", "number": 123}

    def test_output_to_json_invalid(self, capsys):
        """Test handling of invalid JSON"""
        text = "{invalid: json}"
        result = outputToJSON(text)
        assert result is None
        captured = capsys.readouterr()
        assert "Error: Output is not valid JSON!" in captured.out

class TestConversationToText:
    def test_conversation_to_text_basic(self):
        """Test basic conversation formatting"""
        conversation = [
            {"role": "user", "content": "Hello"},
            {"role": "assistant", "content": "Hi there"},
            {"role": "user", "content": "The sky is red"},
        ]
        result = conversationToText(conversation)
        expected = "User: Hello\nAssistant: Hi there\nUser: The sky is red"
        assert result == expected

    def test_conversation_to_text_empty(self):
        """Test empty conversation"""
        result = conversationToText([])
        assert result == ""

class TestFormatQAndAs:
    def test_format_q_and_as_basic(self):
        """Test basic Q&A formatting"""
        q_and_as = {
            "What is your name?": "John",
            "How old are you?": "25"
        }
        result = format_q_and_as(q_and_as)
        expected = "Q: What is your name?\nA: John\n\nQ: How old are you?\nA: 25"
        assert result == expected

    def test_format_q_and_as_empty(self):
        """Test empty Q&A dictionary"""
        result = format_q_and_as({})
        assert result == ""

    def test_format_q_and_as_no_answers(self):
        """Test Q&A formatting with questions but no answers"""
        q_and_as = {
            "What is your name?": "",
            "How old are you?": ""
        }
        result = format_q_and_as(q_and_as)
        expected = "Q: What is your name?\nA: \n\nQ: How old are you?\nA:"
        assert result == expected

class TestFormatQuestion:
    def test_format_question_open_ended(self):
        """Test formatting open-ended question"""
        question = Question("What is your name?")
        result = format_question(question)
        assert result == "What is your name?"

    def test_format_question_mcq(self):
        """Test formatting MCQ question"""
        question = Question(
            "What is your favorite color?",
            "mcq",
            ["Red", "Blue", "Green"]
        )
        result = format_question(question)
        assert "What is your favorite color?" in result
        assert "Multiple Choice" in result
        assert "Red" in result
        assert "Blue" in result
        assert "Green" in result

class TestMatchMCQOption:
    def test_match_mcq_option_exact_match(self):
        """Test exact matching of MCQ options"""
        options = ["Yes", "No", "Maybe"]
        result = match_mcq_option("Yes", options)
        assert result == "Yes"

    def test_match_mcq_option_case_insensitive(self):
        """Test case-insensitive matching"""
        options = ["Yes", "No", "Maybe"]
        result = match_mcq_option("yes", options)
        assert result == "Yes"

    def test_match_mcq_option_substring_in_answer(self):
        """Test when option text is found within answer"""
        options = ["Strongly Agree", "Agree", "Disagree"]
        result = match_mcq_option("I strongly agree with that", options)
        assert result == "Strongly Agree"

    def test_match_mcq_option_answer_in_option(self):
        """Test when answer is found within option text"""
        options = ["Very Happy", "Somewhat Happy", "Not Happy"]
        result = match_mcq_option("Happy", options)
        assert result == "Very Happy"  # Should match first containing "Happy"

    def test_match_mcq_option_no_match(self):
        """Test when no match is found"""
        options = ["Red", "Blue", "Green"]
        result = match_mcq_option("Purple", options)
        assert result == "Purple"