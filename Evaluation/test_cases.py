"""
Test cases for evaluating the different agents.
Contains predefined test cases for Talker, Evaluator, and RAG agents.
"""
import sys
sys.path.append("..\\Backend")  # Add Backend directory to sys.path to allow imports

from test_case import TalkerTestCase, EvaluatorTestCase, SummariserTestCase

# ==================== TALKER AGENT TEST CASES ====================

talker_test_cases = [
    TalkerTestCase(
        name="Talker - Simple open-ended question 1",
        interview_context="Job interview for a software developer position",
        conversation_history=[],
        question={
            "text": "What is your greatest strength?",
            "type": "open_ended"
        },
        expected_answer="Could you tell me about your greatest strength?",
        previous_q_and_a=None
    ),

    TalkerTestCase(
        name="Talker - Simple open-ended question 2",
        interview_context="Sleep quality survey",
        conversation_history=[],
        question={
            "text": "How did you sleep last night?",
            "type": "open_ended"
        },
        expected_answer="How did you sleep last night? Could you describe the quality of your sleep?",
        previous_q_and_a=None
    ),
    
    TalkerTestCase(
        name="Talker - MCQ question with options",
        interview_context="Healthcare questionnaire",
        conversation_history=[],
        question={
            "text": "How would you rate your overall health?",
            "type": "mcq",
            "options": ["Excellent", "Good", "Fair", "Poor"]
        },
        expected_answer="How would you rate your overall health? Would you say excellent, good, fair, or poor?",
        previous_q_and_a=None
    ),
    
    TalkerTestCase(
        name="Talker - Rephrased question based on context",
        interview_context="Job interview",
        conversation_history=[
            {"role": "user", "content": "I'm good at solving complex problems"}
        ],
        question={
            "text": "Could you tell me about a time where you demonstrated this strength?",
            "type": "open_ended"
        },
        expected_answer="I hear you mentioned that you're good at solving complex problems. Could you provide a specific example of a problem you've solved in the past?",
        previous_q_and_a={"What is your greatest strength?": "Problem-solving"}
    ),
    
    TalkerTestCase(
        name="Talker - Rephrasing when answer is unclear",
        interview_context="Sleep quality survey",
        conversation_history=[
            {"role": "user", "content": "I don't know, I was asleep"}
        ],
        question={
            "text": "How did you sleep last night?",
            "type": "open_ended"
        },
        expected_answer="Fair enough. Did you wake up feeling rested, or still tired?",
        previous_q_and_a=None
    ),

    TalkerTestCase(
        name="Talker - MCQ follow-up when answer is ambiguous",
        interview_context="Information collection survey",
        conversation_history=[
            {"role": "user", "content": "I guess I'm doing okay"}
        ],
        question={
            "text": "How would you rate your mood from 1 to 10?",
            "type": "mcq"
        },
        expected_answer="To help us better understand, would you say your mood is closer to 1 (very bad), 5 (neutral), or 10 (very good)?",
        previous_q_and_a=None
    ),

    TalkerTestCase(
        name="Talker - Simple open-ended question 3",
        interview_context="Customer feedback survey",
        conversation_history=[],
        question={
            "text": "What did you like about our service?",
            "type": "open_ended"
        },
        expected_answer="What did you like about our service?",
        previous_q_and_a=None
    ),

    TalkerTestCase(
        name="Talker - Simple open-ended question 4",
        interview_context="University application",
        conversation_history=[],
        question={
            "text": "Why did you choose this course?",
            "type": "open_ended"
        },
        expected_answer="Why did you choose this course?",
        previous_q_and_a=None
    ),

    TalkerTestCase(
        name="Talker - Simple open-ended question 5",
        interview_context="Fitness survey",
        conversation_history=[],
        question={
            "text": "How often do you exercise?",
            "type": "open_ended"
        },
        expected_answer="How often do you exercise?",
        previous_q_and_a=None
    ),

    TalkerTestCase(
        name="Talker - MCQ question 2",
        interview_context="Food preference survey",
        conversation_history=[],
        question={
            "text": "What is your preferred meal time?",
            "type": "mcq",
            "options": ["Breakfast", "Lunch", "Dinner"]
        },
        expected_answer="What is your preferred meal time? Would you say breakfast, lunch, or dinner?",
        previous_q_and_a=None
    ),

    TalkerTestCase(
        name="Talker - MCQ question 3",
        interview_context="Transport survey",
        conversation_history=[],
        question={
            "text": "What is your main mode of transport?",
            "type": "mcq",
            "options": ["Car", "Bus", "Train", "Bike"]
        },
        expected_answer="What is your main mode of transport? Would you say car, bus, train, or bike?",
        previous_q_and_a=None
    ),

    TalkerTestCase(
        name="Talker - MCQ question 4",
        interview_context="Tech usage survey",
        conversation_history=[],
        question={
            "text": "Which device do you use most often?",
            "type": "mcq",
            "options": ["Phone", "Laptop", "Tablet"]
        },
        expected_answer="Which device do you use most often? Would you say phone, laptop, or tablet?",
        previous_q_and_a=None
    ),

    TalkerTestCase(
        name="Talker - Context follow-up 2",
        interview_context="Job interview",
        conversation_history=[
            {"role": "user", "content": "I enjoy working in teams"}
        ],
        question={
            "text": "Can you give an example?",
            "type": "open_ended"
        },
        expected_answer="You mentioned that you enjoy working in teams. Could you give an example of when you worked successfully in a team?",
        previous_q_and_a={"Do you prefer teamwork or individual work?": "Teamwork"}
    ),

    TalkerTestCase(
        name="Talker - Context follow-up 3",
        interview_context="Education survey",
        conversation_history=[
            {"role": "user", "content": "Math is my favourite subject"}
        ],
        question={
            "text": "Why is that?",
            "type": "open_ended"
        },
        expected_answer="You mentioned that math is your favourite subject. Could you explain why you enjoy it?",
        previous_q_and_a=None
    ),

    TalkerTestCase(
        name="Talker - Clarification 1",
        interview_context="Health survey",
        conversation_history=[
            {"role": "user", "content": "Not really sure"}
        ],
        question={
            "text": "How often do you feel stressed?",
            "type": "open_ended"
        },
        expected_answer="That's okay. Would you say you feel stressed often, sometimes, or rarely?",
        previous_q_and_a=None
    ),

    TalkerTestCase(
        name="Talker - Clarification 2",
        interview_context="Lifestyle survey",
        conversation_history=[
            {"role": "user", "content": "Maybe sometimes"}
        ],
        question={
            "text": "Do you eat healthy regularly?",
            "type": "open_ended"
        },
        expected_answer="Would you say you eat healthy food regularly, occasionally, or rarely?",
        previous_q_and_a=None
    ),

    TalkerTestCase(
        name="Talker - MCQ question 4",
        interview_context="Tech usage survey",
        conversation_history=[],
        question={
            "text": "Which device do you use most often?",
            "type": "mcq",
            "options": ["Phone", "Laptop", "Tablet"]
        },
        expected_answer="Which device do you use most often? Would you say phone, laptop, or tablet?",
        previous_q_and_a=None
    ),

    TalkerTestCase(
        name="Talker - Simple repetition 1",
        interview_context="General survey",
        conversation_history=[],
        question={
            "text": "What is your favourite hobby?",
            "type": "open_ended"
        },
        expected_answer="What is your favourite hobby?",
        previous_q_and_a=None
    ),

    TalkerTestCase(
        name="Talker - Simple repetition 2",
        interview_context="Travel survey",
        conversation_history=[],
        question={
            "text": "Where did you last travel?",
            "type": "open_ended"
        },
        expected_answer="Where did you last travel?",
        previous_q_and_a=None
    ),

    TalkerTestCase(
        name="Talker - MCQ minimal phrasing 1",
        interview_context="Shopping survey",
        conversation_history=[],
        question={
            "text": "How often do you shop online?",
            "type": "mcq",
            "options": ["Daily", "Weekly", "Monthly", "Rarely"]
        },
        expected_answer="How often do you shop online? Would you say daily, weekly, monthly, or rarely?",
        previous_q_and_a=None
    ),

    TalkerTestCase(
        name="Talker - MCQ minimal phrasing 2",
        interview_context="Media survey",
        conversation_history=[],
        question={
            "text": "What type of content do you watch most?",
            "type": "mcq",
            "options": ["Movies", "TV shows", "YouTube"]
        },
        expected_answer="What type of content do you watch most? Would you say movies, TV shows, or YouTube?",
        previous_q_and_a=None
    ),

    TalkerTestCase(
        name="Talker - Context reuse simple",
        interview_context="Job interview",
        conversation_history=[
            {"role": "user", "content": "I like learning new technologies"}
        ],
        question={
            "text": "Can you expand on that?",
            "type": "open_ended"
        },
        expected_answer="You mentioned that you like learning new technologies. Could you expand on that?",
        previous_q_and_a=None
    ),

    TalkerTestCase(
        name="Talker - Neutral fallback question",
        interview_context="General survey",
        conversation_history=[
            {"role": "user", "content": ""}
        ],
        question={
            "text": "Can you tell me more?",
            "type": "open_ended"
        },
        expected_answer="Could you tell me more?",
        previous_q_and_a=None
    ),

    TalkerTestCase(
        name="Talker - Simple open-ended question 6",
        interview_context="Customer satisfaction survey",
        conversation_history=[],
        question={
            "text": "How satisfied are you with our service?",
            "type": "open_ended"
        },
        expected_answer="How satisfied are you with our service?",
        previous_q_and_a=None
    ),

    TalkerTestCase(
        name="Talker - Very simple fallback",
        interview_context="General survey",
        conversation_history=[],
        question={
            "text": "Please describe your experience",
            "type": "open_ended"
        },
        expected_answer="Please describe your experience",
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
        question={
            "text": "What is your greatest strength?",
            "type": "open_ended"
        },
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
        question={
            "text": "How did you sleep last night?",
            "type": "open_ended"
        },
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
        question={
            "text": "What is your annual income?",
            "type": "open_ended"
        },
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
        question={
            "text": "How would you rate your overall health?",
            "type": "mcq",
            "options": ["Excellent", "Good", "Fair", "Poor"]
        },
        expected_answer={
            "satisfactory": True,
            "override_skip": False,
            "reasoning": "The user response clearly match the 'good' option from the multiple choices.",
            "follow_up_question": None
        }
    ),
    
    EvaluatorTestCase(
        name="Evaluator - MCQ unclear answer",
        interview_context="Customer feedback",
        conversation_history=[
            {"role": "assistant", "content": "How satisfied are you with our service?"},
            {"role": "user", "content": "It could be better"}
        ],
        question={
            "text": "How satisfied are you with our service?",
            "type": "mcq",
            "options": ["Very Satisfied", "Satisfied", "Neutral", "Dissatisfied", "Very Dissatisfied"]
        },
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
        question={
            "text": "Have you ever been diagnosed with a mental health condition?",
            "type": "mcq",
            "options": ["Yes", "No"]
        },
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
        question={
            "text": "How often do you brush your teeth?",
            "type": "open_ended"
        },
        expected_answer={
            "satisfactory": True,
            "override_skip": False,
            "reasoning": "After follow-up, the user provided a specific answer about brushing frequency.",
            "follow_up_question": None
        }
    ),

    EvaluatorTestCase(
        name="Evaluator - Clear open-ended factual answer",
        interview_context="General survey",
        conversation_history=[
            {"role": "assistant", "content": "What is your favorite color?"},
            {"role": "user", "content": "Blue"}
        ],
        question={
            "text": "What is your favorite color?",
            "type": "open_ended"
        },
        expected_answer={
            "satisfactory": True,
            "override_skip": False,
            "reasoning": "The user provided a clear and direct answer.",
            "follow_up_question": None
        }
    ),

    EvaluatorTestCase(
        name="Evaluator - Vague open-ended answer",
        interview_context="Lifestyle survey",
        conversation_history=[
            {"role": "assistant", "content": "How often do you exercise?"},
            {"role": "user", "content": "Sometimes"}
        ],
        question={
            "text": "How often do you exercise?",
            "type": "open_ended"
        },
        expected_answer={
            "satisfactory": False,
            "override_skip": False,
            "reasoning": "The response is vague and lacks specific frequency details.",
            "follow_up_question": "Could you specify how many times per week you exercise?"
        }
    ),

    EvaluatorTestCase(
        name="Evaluator - Exact MCQ match",
        interview_context="Food survey",
        conversation_history=[
            {"role": "assistant", "content": "Do you prefer tea or coffee?"},
            {"role": "user", "content": "Tea"}
        ],
        question={
            "text": "Do you prefer tea or coffee?",
            "type": "mcq",
            "options": ["Tea", "Coffee"]
        },
        expected_answer={
            "satisfactory": True,
            "override_skip": False,
            "reasoning": "The user selected one of the provided options.",
            "follow_up_question": None
        }
    ),

    EvaluatorTestCase(
        name="Evaluator - MCQ ambiguous synonym",
        interview_context="Food survey",
        conversation_history=[
            {"role": "assistant", "content": "Do you prefer tea or coffee?"},
            {"role": "user", "content": "I like both"}
        ],
        question={
            "text": "Do you prefer tea or coffee?",
            "type": "mcq",
            "options": ["Tea", "Coffee"]
        },
        expected_answer={
            "satisfactory": False,
            "override_skip": False,
            "reasoning": "The response does not clearly select one of the provided options.",
            "follow_up_question": "Which do you prefer more, tea or coffee?"
        }
    ),

    EvaluatorTestCase(
        name="Evaluator - User refusal general",
        interview_context="Profile form",
        conversation_history=[
            {"role": "assistant", "content": "What is your phone number?"},
            {"role": "user", "content": "I'd rather not say"}
        ],
        question={
            "text": "What is your phone number?",
            "type": "open_ended"
        },
        expected_answer={
            "satisfactory": False,
            "override_skip": True,
            "reasoning": "The user refused to provide the information.",
            "follow_up_question": None
        }
    ),

    EvaluatorTestCase(
        name="Evaluator - Partial but sufficient open-ended",
        interview_context="Travel survey",
        conversation_history=[
            {"role": "assistant", "content": "Where did you travel last year?"},
            {"role": "user", "content": "I went to Spain"}
        ],
        question={
            "text": "Where did you travel last year?",
            "type": "open_ended"
        },
        expected_answer={
            "satisfactory": True,
            "override_skip": False,
            "reasoning": "The user provided a clear destination.",
            "follow_up_question": None
        }
    ),

    EvaluatorTestCase(
        name="Evaluator - Open-ended unclear intent",
        interview_context="Work survey",
        conversation_history=[
            {"role": "assistant", "content": "What do you do for work?"},
            {"role": "user", "content": "Things"}
        ],
        question={
            "text": "What do you do for work?",
            "type": "open_ended"
        },
        expected_answer={
            "satisfactory": False,
            "override_skip": False,
            "reasoning": "The answer is too vague to understand the user's occupation.",
            "follow_up_question": "Could you describe your job role in more detail?"
        }
    ),

    EvaluatorTestCase(
        name="Evaluator - MCQ exact lowercase match",
        interview_context="Health survey",
        conversation_history=[
            {"role": "assistant", "content": "Do you smoke? Yes or No"},
            {"role": "user", "content": "no"}
        ],
        question={
            "text": "Do you smoke?",
            "type": "mcq",
            "options": ["Yes", "No"]
        },
        expected_answer={
            "satisfactory": True,
            "override_skip": False,
            "reasoning": "The answer matches one of the provided options.",
            "follow_up_question": None
        }
    ),

    EvaluatorTestCase(
        name="Evaluator - MCQ unclear phrasing",
        interview_context="Fitness survey",
        conversation_history=[
            {"role": "assistant", "content": "Do you exercise regularly? Yes or No"},
            {"role": "user", "content": "Kind of"}
        ],
        question={
            "text": "Do you exercise regularly?",
            "type": "mcq",
            "options": ["Yes", "No"]
        },
        expected_answer={
            "satisfactory": False,
            "override_skip": False,
            "reasoning": "The response does not clearly match the available options.",
            "follow_up_question": "Would you say yes or no?"
        }
    ),

    EvaluatorTestCase(
        name="Evaluator - Detailed open-ended answer",
        interview_context="Education survey",
        conversation_history=[
            {"role": "assistant", "content": "What did you study?"},
            {"role": "user", "content": "Computer Science with a focus on AI"}
        ],
        question={
            "text": "What did you study?",
            "type": "open_ended"
        },
        expected_answer={
            "satisfactory": True,
            "override_skip": False,
            "reasoning": "The user provided a clear and detailed answer.",
            "follow_up_question": None
        }
    ),

    EvaluatorTestCase(
        name="Evaluator - Open-ended non-answer",
        interview_context="Feedback form",
        conversation_history=[
            {"role": "assistant", "content": "What did you like about the product?"},
            {"role": "user", "content": "Nothing"}
        ],
        question={
            "text": "What did you like about the product?",
            "type": "open_ended"
        },
        expected_answer={
            "satisfactory": False,
            "override_skip": False,
            "reasoning": "The user did not clearly indicate if they actually don't find a thing they like about the product.",
            "follow_up_question": None
        }
    ),

    EvaluatorTestCase(
        name="Evaluator - Ambiguous opinion",
        interview_context="Service feedback",
        conversation_history=[
            {"role": "assistant", "content": "How was the service?"},
            {"role": "user", "content": "Okay"}
        ],
        question={
            "text": "How was the service?",
            "type": "open_ended"
        },
        expected_answer={
            "satisfactory": False,
            "override_skip": False,
            "reasoning": "The response is vague and lacks detail.",
            "follow_up_question": "Could you describe what made it okay?"
        }
    ),

    EvaluatorTestCase(
        name="Evaluator - Strong refusal wording",
        interview_context="Sensitive survey",
        conversation_history=[
            {"role": "assistant", "content": "What is your age?"},
            {"role": "user", "content": "I don't want to answer that"}
        ],
        question={
            "text": "What is your age?",
            "type": "open_ended"
        },
        expected_answer={
            "satisfactory": False,
            "override_skip": True,
            "reasoning": "The user explicitly refused to answer.",
            "follow_up_question": None
        }
    ),

    EvaluatorTestCase(
        name="Evaluator - Multi-step clarification resolved",
        interview_context="Diet survey",
        conversation_history=[
            {"role": "assistant", "content": "Do you follow a specific diet?"},
            {"role": "user", "content": "Yes"},
            {"role": "assistant", "content": "Which one?"},
            {"role": "user", "content": "Vegetarian"}
        ],
        question={
            "text": "Do you follow a specific diet?",
            "type": "open_ended"
        },
        expected_answer={
            "satisfactory": True,
            "override_skip": False,
            "reasoning": "After clarification, the user provided a specific diet.",
            "follow_up_question": None
        }
    ),

    EvaluatorTestCase(
        name="Evaluator - MCQ with extra words but valid",
        interview_context="Transport survey",
        conversation_history=[
            {"role": "assistant", "content": "Do you use public transport? Yes or No"},
            {"role": "user", "content": "Yes, every day"}
        ],
        question={
            "text": "Do you use public transport?",
            "type": "mcq",
            "options": ["Yes", "No"]
        },
        expected_answer={
            "satisfactory": True,
            "override_skip": False,
            "reasoning": "The response clearly includes one of the valid options.",
            "follow_up_question": None
        }
    ),

    EvaluatorTestCase(
        name="Evaluator - MCQ indirect answer",
        interview_context="Transport survey",
        conversation_history=[
            {"role": "assistant", "content": "Do you use public transport? Yes or No"},
            {"role": "user", "content": "Only sometimes"}
        ],
        question={
            "text": "Do you use public transport?",
            "type": "mcq",
            "options": ["Yes", "No"]
        },
        expected_answer={
            "satisfactory": True,
            "override_skip": False,
            "reasoning": "The response clearly infers they use public transport.",
            "follow_up_question": None
        }
    ),

    EvaluatorTestCase(
        name="Evaluator - Clear numeric answer",
        interview_context="Fitness tracking",
        conversation_history=[
            {"role": "assistant", "content": "How many hours do you sleep?"},
            {"role": "user", "content": "7 hours"}
        ],
        question={
            "text": "How many hours do you sleep?",
            "type": "open_ended"
        },
        expected_answer={
            "satisfactory": True,
            "override_skip": False,
            "reasoning": "The user provided a clear numeric answer.",
            "follow_up_question": None
        }
    ),

    EvaluatorTestCase(
        name="Evaluator - Missing answer",
        interview_context="General survey",
        conversation_history=[
            {"role": "assistant", "content": "What is your hobby?"},
            {"role": "user", "content": ""}
        ],
        question={
            "text": "What is your hobby?",
            "type": "open_ended"
        },
        expected_answer={
            "satisfactory": False,
            "override_skip": False,
            "reasoning": "No answer was provided.",
            "follow_up_question": "Could you tell me about your hobbies?"
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
    ),

    SummariserTestCase(
        name="RAG - Simple factual extraction",
        interview_context="Basic info",
        conversation_history=[
            {"role": "assistant", "content": "What city do you live in?"},
            {"role": "user", "content": "I live in London"}
        ],
        question="What city do you live in?",
        expected_answer="London",
        question_type="open_ended"
    ),

    SummariserTestCase(
        name="RAG - Short profession extraction",
        interview_context="Employment",
        conversation_history=[
            {"role": "assistant", "content": "What is your job?"},
            {"role": "user", "content": "I'm a teacher"}
        ],
        question="What is your job?",
        expected_answer="Teacher",
        question_type="open_ended"
    ),

    SummariserTestCase(
        name="RAG - Simple yes answer",
        interview_context="Health survey",
        conversation_history=[
            {"role": "assistant", "content": "Do you smoke?"},
            {"role": "user", "content": "No"}
        ],
        question="Do you smoke?",
        expected_answer="No",
        question_type="mcq",
        options=["Yes", "No"]
    ),

    SummariserTestCase(
        name="RAG - Simple no answer with extra words",
        interview_context="Health survey",
        conversation_history=[
            {"role": "assistant", "content": "Do you drink alcohol?"},
            {"role": "user", "content": "No, not at all"}
        ],
        question="Do you drink alcohol?",
        expected_answer="No",
        question_type="mcq",
        options=["Yes", "No"]
    ),

    SummariserTestCase(
        name="RAG - Extract number clearly",
        interview_context="Fitness",
        conversation_history=[
            {"role": "assistant", "content": "How many times a week do you exercise?"},
            {"role": "user", "content": "About 3 times a week"}
        ],
        question="How many times a week do you exercise?",
        expected_answer="3 times a week",
        question_type="open_ended"
    ),

    SummariserTestCase(
        name="RAG - Extract duration",
        interview_context="Work experience",
        conversation_history=[
            {"role": "assistant", "content": "How long have you worked here?"},
            {"role": "user", "content": "I've been here for 2 years"}
        ],
        question="How long have you worked here?",
        expected_answer="2 years",
        question_type="open_ended"
    ),

    SummariserTestCase(
        name="RAG - Clear hobby extraction",
        interview_context="Personal profile",
        conversation_history=[
            {"role": "assistant", "content": "What are your hobbies?"},
            {"role": "user", "content": "I enjoy reading books"}
        ],
        question="What are your hobbies?",
        expected_answer="Reading books",
        question_type="open_ended"
    ),

    SummariserTestCase(
        name="RAG - Extract food preference",
        interview_context="Food survey",
        conversation_history=[
            {"role": "assistant", "content": "What is your favorite food?"},
            {"role": "user", "content": "Pizza"}
        ],
        question="What is your favorite food?",
        expected_answer="Pizza",
        question_type="open_ended"
    ),

    SummariserTestCase(
        name="RAG - Extract simple preference sentence",
        interview_context="Music survey",
        conversation_history=[
            {"role": "assistant", "content": "What kind of music do you like?"},
            {"role": "user", "content": "I like pop music"}
        ],
        question="What kind of music do you like?",
        expected_answer="Pop music",
        question_type="open_ended"
    ),

    SummariserTestCase(
        name="RAG - MCQ direct match",
        interview_context="Transport survey",
        conversation_history=[
            {"role": "assistant", "content": "Do you own a car?"},
            {"role": "user", "content": "Yes"}
        ],
        question="Do you own a car?",
        expected_answer="Yes",
        question_type="mcq",
        options=["Yes", "No"]
    ),

    SummariserTestCase(
        name="RAG - Extract simple location",
        interview_context="Travel",
        conversation_history=[
            {"role": "assistant", "content": "Where did you go on holiday?"},
            {"role": "user", "content": "I went to Italy"}
        ],
        question="Where did you go on holiday?",
        expected_answer="Italy",
        question_type="open_ended"
    ),

    SummariserTestCase(
        name="RAG - Extract simple language",
        interview_context="Language survey",
        conversation_history=[
            {"role": "assistant", "content": "What language do you speak?"},
            {"role": "user", "content": "English"}
        ],
        question="What language do you speak?",
        expected_answer="English",
        question_type="open_ended"
    ),

    SummariserTestCase(
        name="RAG - Extract pet ownership",
        interview_context="Lifestyle",
        conversation_history=[
            {"role": "assistant", "content": "Do you have pets?"},
            {"role": "user", "content": "Yes, I have a dog"}
        ],
        question="Do you have pets?",
        expected_answer="Yes",
        question_type="mcq",
        options=["Yes", "No"]
    ),

    SummariserTestCase(
        name="RAG - Extract specific pet",
        interview_context="Lifestyle",
        conversation_history=[
            {"role": "assistant", "content": "What pet do you have?"},
            {"role": "user", "content": "I have a dog"}
        ],
        question="What pet do you have?",
        expected_answer="Dog",
        question_type="open_ended"
    ),

    SummariserTestCase(
        name="RAG - Extract simple education",
        interview_context="Education",
        conversation_history=[
            {"role": "assistant", "content": "What is your highest qualification?"},
            {"role": "user", "content": "I have a bachelor's degree"}
        ],
        question="What is your highest qualification?",
        expected_answer="Bachelor's degree",
        question_type="open_ended"
    ),

    SummariserTestCase(
        name="RAG - Extract working status",
        interview_context="Employment",
        conversation_history=[
            {"role": "assistant", "content": "Are you currently employed?"},
            {"role": "user", "content": "Yes, I am employed full-time"}
        ],
        question="Are you currently employed?",
        expected_answer="Yes",
        question_type="mcq",
        options=["Yes", "No"]
    ),

    SummariserTestCase(
        name="RAG - Extract simple rating to MCQ",
        interview_context="Service feedback",
        conversation_history=[
            {"role": "assistant", "content": "How was the service?"},
            {"role": "user", "content": "It was good"}
        ],
        question="How was the service?",
        expected_answer="Good",
        question_type="mcq",
        options=["Excellent", "Good", "Fair", "Poor"]
    ),

    SummariserTestCase(
        name="RAG - Extract clear binary preference",
        interview_context="Preference survey",
        conversation_history=[
            {"role": "assistant", "content": "Do you prefer mornings or evenings?"},
            {"role": "user", "content": "Mornings"}
        ],
        question="Do you prefer mornings or evenings?",
        expected_answer="Mornings",
        question_type="open_ended"
    )
]