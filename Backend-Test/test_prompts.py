import sys
sys.path.append("..\\Backend")  # Add Backend directory to sys.path to allow imports

from prompts import (
    Talker_system_prompt,
    Talker_ask_question_prompt,
    Talker_follow_up_question_prompt,
    Talker_closing_statement_prompt,
    Evaluator_system_prompt,
    RAG_system_prompt,
    RAG_collate_answer,
    RAG_summarise_conversation
)

class TestTalkerPrompts:
    def test_talker_system_prompt(self):
        """Test Talker system prompt generation"""
        context = "Test interview context"
        result = Talker_system_prompt(context)
        
        assert context in result

    def test_talker_ask_question_prompt_without_history(self):
        """Test ask question prompt without history"""
        question = "What is your name?"
        result = Talker_ask_question_prompt(question)
        
        assert question in result
        assert "START OF INTERVIEW" in result
        assert "No responses from the user yet" in result

    def test_talker_ask_question_prompt_with_history(self):
        """Test ask question prompt with history"""
        question = "What is your age?"
        previous_q_and_a = "Q: What is your name?\nA: John"
        last_message = "I'm 25"
        
        result = Talker_ask_question_prompt(question, previous_q_and_a, last_message)
        
        assert question in result
        assert last_message in result
        assert previous_q_and_a in result
        assert "No responses from the user yet" not in result

    def test_talker_follow_up_question_prompt(self):
        """Test follow-up question prompt"""
        question = "What is your name?"
        reasoning = "Answer too brief"
        transcript = "User: John"
        previous_q_and_a = "Q: What is your age?\nA: 25"
        follow_up = "Can you provide your full name?"
        
        result = Talker_follow_up_question_prompt(
            question, reasoning, transcript, previous_q_and_a, follow_up
        )
        
        assert question in result
        assert reasoning in result
        assert transcript in result
        assert follow_up in result
        assert previous_q_and_a in result

    def test_talker_closing_statement_prompt(self):
        """Test closing statement prompt"""
        result = Talker_closing_statement_prompt()
        
        assert result is not None

class TestEvaluatorPrompt:
    def test_evaluator_system_prompt(self):
        """Test evaluator system prompt"""
        context = "Medical interview"
        question = "How are you feeling?"
        transcript = "User: I'm feeling tired"
        
        result = Evaluator_system_prompt(context, question, transcript)
        
        assert context in result
        assert question in result
        assert transcript in result
        
        assert "Completeness" in result
        assert "Relevance" in result
        assert "Clarity" in result
        assert "Option Match" in result
        assert "User Preference" in result

        assert "ANALYSIS FORMAT INSTRUCTIONS" in result
        assert "satisfactory" in result
        assert "override_skip" in result
        assert "reasoning" in result
        assert "follow_up_question" in result

class TestRAGPrompts:
    def test_rag_system_prompt(self):
        """Test RAG system prompt"""
        context = "Job interview"
        result = RAG_system_prompt(context)
        
        assert context in result

    def test_rag_collate_answer_open_ended(self):
        """Test RAG collate answer for open-ended questions"""
        conversation = "User: My name is John\nAssistant: Nice to meet you"
        question = "What is your name?"
        
        result = RAG_collate_answer(conversation, question)
        
        assert conversation in result
        assert question in result
        assert "multiple-choice" not in result.lower()

    def test_rag_collate_answer_mcq(self):
        """Test RAG collate answer for MCQ questions"""
        conversation = "User: I prefer the red one"
        question = "Which color do you prefer?"
        options = ["Red", "Blue", "Green"]
        
        result = RAG_collate_answer(
            conversation, question, 
            question_type="mcq", 
            options=options
        )
        
        assert conversation in result
        assert question in result
        assert "multiple-choice" in result
        assert "Red" in result
        assert "Blue" in result
        assert "Green" in result

    def test_rag_summarise_conversation(self):
        """Test RAG summarise conversation"""
        conversation = "User: Hello\nAssistant: Hi\nUser: How are you?"
        result = RAG_summarise_conversation(conversation)
        
        assert "Summarise" in result
        assert conversation in result
        assert "Summary:" in result