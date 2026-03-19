"""
Test cases for evaluating the different agents.
Contains predefined test cases for Talker, Evaluator, and RAG agents.
"""
import sys
sys.path.append("..\\Backend")  # Add parent directory to sys.path to allow imports from Backend

from test_case import TalkerTestCase, EvaluatorTestCase, SummariserTestCase
from agents import Question

# ==================== TALKER AGENT TEST CASES ====================

talker_test_cases = [
    TalkerTestCase(
        name="Talker - Simple open-ended question 1",
        interview_context="Job interview for a software developer position",
        conversation_history=[],
        question=Question(
            text="What is your greatest strength?",
            question_type="open_ended"
        ),
        expected_answer="Could you tell me about your greatest strength?",
        previous_q_and_a=None
    ),

    TalkerTestCase(
        name="Talker - Simple open-ended question 2",
        interview_context="Sleep quality survey",
        conversation_history=[],
        question=Question(
            text="How did you sleep last night?",
            question_type="open_ended"
        ),
        expected_answer="How did you sleep last night? Could you describe the quality of your sleep?",
        previous_q_and_a=None
    ),
    
    TalkerTestCase(
        name="Talker - MCQ question with options",
        interview_context="Healthcare questionnaire",
        conversation_history=[],
        question=Question(
            text="How would you rate your overall health?",
            question_type="mcq",
            options=["Excellent", "Good", "Fair", "Poor"]
        ),
        expected_answer="How would you rate your overall health? Would you say excellent, good, fair, or poor?",
        previous_q_and_a=None
    ),
    
    TalkerTestCase(
        name="Talker - Rephrased question based on context",
        interview_context="Job interview",
        conversation_history=[
            {"role": "user", "content": "I'm good at solving complex problems"}
        ],
        question=Question(
            text="Could you tell me about a time where you demonstrated this strength?",
            question_type="open_ended"
        ),
        expected_answer="I hear you mentioned that you're good at solving complex problems. Could you provide a specific example of a problem you've solved in the past?",
        previous_q_and_a={"What is your greatest strength?": "Problem-solving"}
    ),
    
    TalkerTestCase(
        name="Talker - Rephrasing when answer is unclear",
        interview_context="Sleep quality survey",
        conversation_history=[
            {"role": "user", "content": "I don't know, I was asleep"}
        ],
        question=Question(
            text="How did you sleep last night?",
            question_type="open_ended"
        ),
        expected_answer="Fair enough. Did you wake up feeling rested, or still tired?",
        previous_q_and_a=None
    ),

    TalkerTestCase(
        name="Talker - MCQ follow-up when answer is ambiguous",
        interview_context="Information collection survey",
        conversation_history=[
            {"role": "user", "content": "I guess I'm doing okay"}
        ],
        question=Question(
            text="How would you rate your mood from 1 to 10?",
            question_type="mcq",
        ),
        expected_answer="To help us better understand, would you say your mood is closer to 1 (very bad), 5 (neutral), or 10 (very good)?",
        previous_q_and_a=None
    )
]

# ==================== EVALUATOR AGENT TEST CASES ====================

evaluator_test_cases = [
    EvaluatorTestCase(
        name="Evaluator - Complete satisfactory answer",
        interview_context="Job interview",
        conversation_history=[
            {"role": "assistant", "content": "What is your greatest strength?"},
            {"role": "user", "content": "I would say my greatest strength is my ability to solve complex problems quickly and efficiently."}
        ],
        question=Question(
            text="What is your greatest strength?",
            question_type="open_ended"
        ),
        expected_answer={
            "satisfactory": True,
            "override_skip": False,
            "reasoning": "The user provided a clear, complete answer about their problem-solving abilities.",
            "follow_up_question": None
        }
    ),
    
    EvaluatorTestCase(
        name="Evaluator - Unclear answer that needs follow-up",
        interview_context="Sleep quality survey",
        conversation_history=[
            {"role": "assistant", "content": "How did you sleep last night?"},
            {"role": "user", "content": "I don't know, I was asleep"}
        ],
        question=Question(
            text="How did you sleep last night?",
            question_type="open_ended"
        ),
        expected_answer={
            "satisfactory": False,
            "override_skip": False,
            "reasoning": "The user's answer is vague and doesn't provide any information about the quality of their sleep.",
            "follow_up_question": "Did you wake up feeling rested, or still tired?"
        }
    ),

    EvaluatorTestCase(
        name="Evaluator - User wants to skip question",
        interview_context="Personal questionnaire",
        conversation_history=[
            {"role": "assistant", "content": "What is your annual income?"},
            {"role": "user", "content": "I'd rather not share that information"}
        ],
        question=Question(
            text="What is your annual income?",
            question_type="open_ended"
        ),
        expected_answer={
            "satisfactory": False,
            "override_skip": True,
            "reasoning": "The user explicitly stated they don't want to share this information, so we should respect their preference and skip the question.",
            "follow_up_question": None
        }
    ),
    
    EvaluatorTestCase(
        name="Evaluator - MCQ correct match",
        interview_context="Healthcare survey",
        conversation_history=[
            {"role": "assistant", "content": "How would you rate your overall health? Would you say excellent, good, fair, or poor?"},
            {"role": "user", "content": "I'd say I'm in pretty good shape actually"}
        ],
        question=Question(
            text="How would you rate your overall health?",
            question_type="mcq",
            options=["Excellent", "Good", "Fair", "Poor"]
        ),
        expected_answer={
            "satisfactory": False,
            "override_skip": False,
            "reasoning": "The user's response is positive but doesn't clearly match one of the provided MCQ options. We should ask a follow-up to clarify.",
            "follow_up_question": "So would you say your health is good?"
        }
    ),
    
    EvaluatorTestCase(
        name="Evaluator - MCQ unclear answer",
        interview_context="Customer feedback",
        conversation_history=[
            {"role": "assistant", "content": "How satisfied are you with our service?"},
            {"role": "user", "content": "It could be better"}
        ],
        question=Question(
            text="How satisfied are you with our service?",
            question_type="mcq",
            options=["Very Satisfied", "Satisfied", "Neutral", "Dissatisfied", "Very Dissatisfied"]
        ),
        expected_answer={
            "satisfactory": False,
            "override_skip": False,
            "reasoning": "The response 'could be better' is ambiguous and doesn't clearly match any of the MCQ options.",
            "follow_up_question": "To help us better understand, would you say you're satisfied, neutral, or dissatisfied with our service?"
        }
    ),
        
    EvaluatorTestCase(
        name="Evaluator - User uncomfortable",
        interview_context="Medical history",
        conversation_history=[
            {"role": "assistant", "content": "Have you ever been diagnosed with a mental health condition?"},
            {"role": "user", "content": "I'm not comfortable discussing that"}
        ],
        question=Question(
            text="Have you ever been diagnosed with a mental health condition?",
            question_type="mcq",
            options=["Yes", "No"]
        ),
        expected_answer={
            "satisfactory": False,
            "override_skip": True,
            "reasoning": "The user expressed discomfort with the question, so we should respect their feelings and skip the question.",
            "follow_up_question": None
        }
    ),
    
    EvaluatorTestCase(
        name="Evaluator - Multiple follow-ups needed",
        interview_context="Dental intake form",
        conversation_history=[
            {"role": "assistant", "content": "How often do you brush your teeth?"},
            {"role": "user", "content": "Regularly"},
            {"role": "assistant", "content": "Could you be more specific about how many times per day?"},
            {"role": "user", "content": "Usually twice, sometimes once if I'm tired"}
        ],
        question=Question(
            text="How often do you brush your teeth?",
            question_type="open_ended"
        ),
        expected_answer={
            "satisfactory": True,
            "override_skip": False,
            "reasoning": "After follow-up, the user provided a specific answer about brushing frequency.",
            "follow_up_question": None
        }
    )
]

# ==================== RAG/SUMMARISER AGENT TEST CASES ====================

summariser_test_cases = [
    SummariserTestCase(
        name="RAG - Simple answer extraction",
        interview_context="Job application",
        conversation_history=[
            {"role": "assistant", "content": "What is your full name?"},
            {"role": "user", "content": "My name is John Michael Smith"}
        ],
        question="What is your full name?",
        expected_answer="John Michael Smith",
        question_type="open_ended"
    ),
    
    SummariserTestCase(
        name="RAG - Extract from verbose answer",
        interview_context="Medical history",
        conversation_history=[
            {"role": "assistant", "content": "Do you have any allergies?"},
            {"role": "user", "content": "Well, let me think. I remember when I was a kid, I had a bad reaction to penicillin. Also, I'm pretty sure I'm allergic to cats because I always sneeze around them."}
        ],
        question="Do you have any allergies?",
        expected_answer="I am allergic to penicillin and cats.",
        question_type="open_ended"
    ),
    
    SummariserTestCase(
        name="RAG - Extract from conversation with follow-ups",
        interview_context="Employment history",
        conversation_history=[
            {"role": "assistant", "content": "What is your current job?"},
            {"role": "user", "content": "I work as a software engineer"},
            {"role": "assistant", "content": "How long have you been in this role?"},
            {"role": "user", "content": "About three years now"}
        ],
        question="What is your current job and how long have you had it?",
        expected_answer="I am a software engineer and I have been in this role for about three years.",
        question_type="open_ended"
    ),
    
    SummariserTestCase(
        name="RAG - MCQ option matching",
        interview_context="Health survey",
        conversation_history=[
            {"role": "assistant", "content": "How would you describe your sleep quality?"},
            {"role": "user", "content": "Most nights I sleep pretty well, but sometimes I have trouble"}
        ],
        question="How would you describe your sleep quality?",
        expected_answer="Good",  # Should match one of the MCQ options
        question_type="mcq",
        options=["Excellent", "Good", "Fair", "Poor"]
    ),
    
    SummariserTestCase(
        name="RAG - Handle multiple pieces of information",
        interview_context="Dental intake",
        conversation_history=[
            {"role": "assistant", "content": "How often do you visit the dentist?"},
            {"role": "user", "content": "I try to go every six months"},
            {"role": "assistant", "content": "When was your last visit?"},
            {"role": "user", "content": "About three months ago"}
        ],
        question="Dental visit frequency and last visit date",
        expected_answer="I visit the dentist every six months and my last visit was about three months ago.",
        question_type="open_ended"
    ),
    
    SummariserTestCase(
        name="RAG - Don't hallucinate information",
        interview_context="Personal information",
        conversation_history=[
            {"role": "assistant", "content": "What is your age?"},
            {"role": "user", "content": "I'm in my thirties"}
        ],
        question="What is your exact age?",
        expected_answer="I am in my thirties",  # Should not make up an exact number
        question_type="open_ended"
    ),
    
    SummariserTestCase(
        name="RAG - Handle preferences and ratings",
        interview_context="Movie preference survey",
        conversation_history=[
            {"role": "assistant", "content": "What's your favorite movie genre?"},
            {"role": "user", "content": "I really enjoy science fiction, especially ones about space exploration"}
        ],
        question="Favorite movie genre",
        expected_answer="Science fiction, particularly space exploration themes",
        question_type="open_ended"
    )
]

# ==================== COMPREHENSIVE INTEGRATION TEST CASES ====================

integration_test_cases = [
    {
        "name": "Complete interview flow - Job application",
        "interview_context": "Entry-level software developer position interview",
        "questions": [
            Question("What is your name?", "open_ended"),
            Question("What programming languages do you know?", "open_ended"),
            Question("Do you have any prior work experience?", "mcq", ["Yes", "No"]),
            Question("Why do you want to work here?", "open_ended")
        ],
        "user_responses": [
            "My name is Sarah Chen",
            "I know Python and JavaScript, and I'm learning Java",
            "Yes, I did an internship last summer",
            "I'm passionate about technology and your company's mission aligns with my values"
        ],
        "expected_final_answers": {
            "What is your name?": "Sarah Chen",
            "What programming languages do you know?": "Python, JavaScript, and learning Java",
            "Do you have any prior work experience?": "Yes",
            "Why do you want to work here?": "Passionate about technology and aligned with company mission"
        }
    },
    
    {
        "name": "Complete interview flow - Medical intake with sensitive questions",
        "interview_context": "New patient medical history questionnaire",
        "questions": [
            Question("What is your full name?", "open_ended"),
            Question("Do you have any allergies?", "open_ended"),
            Question("Have you ever been hospitalized?", "mcq", ["Yes", "No", "Prefer not to say"]),
            Question("Do you smoke?", "mcq", ["Yes", "No", "Occasionally"])
        ],
        "user_responses": [
            "Robert Johnson",
            "I'm allergic to penicillin",
            "I'd rather not say",
            "I used to but I quit last year"
        ],
        "expected_final_answers": {
            "What is your full name?": "Robert Johnson",
            "Do you have any allergies?": "Allergic to penicillin",
            "Have you ever been hospitalized?": "Prefer not to say",
            "Do you smoke?": "No"  # Should interpret "used to but quit" as No
        }
    }
]