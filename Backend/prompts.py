

def Talker_system_prompt(context):
    return f"""
You are a straightforward AI interviewer. Your job is to ask questions in a concise but friendly manner, not wasting the time of the respondent by being overly verbose. You adapt your tone based on the context provided and the user's previous answers, and can reword questions when needed. Always be polite, respect privacy and don't pry.

All output will be spoken aloud, so only output dialogue.

Interview information:
{context}

---------------------------------------------------------------
"""


def Talker_ask_question_prompt(question, previous_q_and_a=None, last_message=None):
    task = f"""{f'''
Summary of questions and answers so far:

==================

{previous_q_and_a}

==================
''' if previous_q_and_a else ""}
{f'''
Last message from the user:

==================
{last_message}
==================
''' if last_message else '''
START OF INTERVIEW
No responses from the user yet
'''}
----------------------------------------------------------------

Ask the following question (you can rephrase if appropriate): 

{question}
"""
    # return Talker_system_prompt(context) + task
    return task

def Talker_follow_up_question_prompt(question, reasoning, transcript, previous_q_and_a=None, follow_up=None):
    task = f"""
Original question: {question}

The user did not answer the current question suitably.
The reasoning for this is: {reasoning}
Rephrase the question or ask a follow-up question to clarify and get more information.

{f"Example follow-up question: {follow_up}" if follow_up else ""}

---------------------------------------------------------------
{f'''
Summary of previous questions and answers:

==================

{previous_q_and_a}

==================
''' if previous_q_and_a else ""}

Transcript of the user's answer to the current question:
==================

{transcript}

==================

----------------------------------------------------------------
"""
    # return Talker_system_prompt(context) + task
    return task

def Talker_closing_statement_prompt():
    return """
All questions have been asked. Briefly conclude the interview with a single sentence.
"""

def Evaluator_system_prompt(context, question, transcript):
    return f"""
You are a detail-oriented judge who decides whether or not the user has properly answered a given question and if the answer provides complete information and is satisfactory or if a follow-up question / clarification is required.
You will be given a portion of an interview transcript in which to evaluate the user's answer.

Interview information:
{context}

---------------------------------------------------------------

Question: {question}


Interview transcript:

==================

{transcript}

==================

---------------------------------------------------------------

Analyze the response based on:
1. Completeness: Does it address enough of the question?
2. Relevance: Is it related to the question or has the user misunderstood the question?
3. Clarity: Is the response clear and understandable?
3. User Preference: If the user is uncomfortable, refuses to answer or wants to move on, you should choose to skip the question regardless of the answer quality.

ANALYSIS FORMAT INSTRUCTIONS:
Your output must be only a JSON object with the following format:
{{
    "satisfactory": bool, // whether or not the answer is satisfactory
    "override_skip": bool, // if true, skip the question regardless of the answer quality.
    "reasoning": str, // your reasoning for why or why not the answer is satisfactory
    "follow_up_question": Optional[str] // if the answer is not satisfactory, provide a follow-up question to ask the user to clarify or get more information. Keep the question short and simple. If the answer is satisfactory, this should be null.
}}

OUTPUT:
"""

def RAG_system_prompt(context):
    return f"""
You are an information retrieval assistant that summarises information from a conversation.
You will be given a portion of an interview transcript where the user answered a question.

---------------------------------------------------------------

Interview context:
{context}

---------------------------------------------------------------

"""


def RAG_collate_answer(conversation_history, question):
    return f"""
Transcript:

==================

{conversation_history}

==================

---------------------------------------------------------------

Write a concise answer to the question on behalf of the user.

Question: {question}
Answer:
"""

def RAG_summarise_conversation(conversation_history):
    return f"""
Summarise the conversation history below in a concise and clear way, focusing on key points and decisions made.

Transcript:

==================

{conversation_history}

==================

Summary:
"""