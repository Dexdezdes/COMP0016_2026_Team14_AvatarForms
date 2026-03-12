
import json
import re

def clean_script(text):
    # Remove LLM tags and any text within brackets (), [], {}
    # Remove emojis
    text = LLM_strip(text)
    text = bracketStrip(text)
    text = emojiStrip(text)
    return text

# def emojiStrip(text):
#     return ''.join(c for c in text if c.isalnum() or c.isspace() or c in ['.', ',', '!', '?', ':', ';', '-', '_', '"', "'"])

def emojiStrip(text):

    emoji_pattern = re.compile("["
        u"\U0001F600-\U0001F64F"  # emoticons
        u"\U0001F300-\U0001F5FF"  # symbols & pictographs
        u"\U0001F680-\U0001F6FF"  # transport & map symbols
        u"\U0001F1E0-\U0001F1FF"  # flags (iOS)
        u"\U0001F1F2-\U0001F1F4"  # Macau flag
        u"\U0001F1E6-\U0001F1FF"  # flags
        u"\U0001F600-\U0001F64F"
        u"\U00002702-\U000027B0"
        u"\U000024C2-\U0001F251"
        u"\U0001f926-\U0001f937"
        u"\U0001F1F2"
        u"\U0001F1F4"
        u"\U0001F620"
        u"\u200d"
        u"\u2640-\u2642"
        "]+", flags=re.UNICODE)

    text = emoji_pattern.sub(r'', text)
    return text

def LLM_strip(text):
    text = re.sub(r"<[^>]*>.*?</[^>]*>", "", text, flags=re.DOTALL)
    return text.strip()

def bracketStrip(text):
    tag = [["(", ")"], ["[", "]"], ["{", "}"]]
    for t in tag:
        text = tagStrip(text, t[0], t[1])
    return text

def tagStrip(text, start_tag, end_tag):
    while start_tag in text and end_tag in text:
        start = text.index(start_tag)
        end = text.index(end_tag)
        text = text[:start] + text[end + len(end_tag):]
    return text.strip()

def outputToJSON(text):
    try:
        text = LLM_strip(text)
        return json.loads(text)
    except json.JSONDecodeError:
        print("\033[91mError: Output is not valid JSON!\033[0m")
        print(f"Received output: {text}")
        return None

def conversationToText(conversation):
    return "\n".join([f"{message['role'].capitalize()}: {message['content']}" for message in conversation])

def format_q_and_as(q_and_as):
    text = ""
    for question, answer in q_and_as.items():
        text += f"Q: {question}\nA: {answer}\n\n"
    return text.strip()

class bcolors:
    HEADER = '\033[95m'
    OKBLUE = '\033[94m'
    OKCYAN = '\033[96m'
    OKGREEN = '\033[92m'
    WARNING = '\033[93m'
    FAIL = '\033[91m'
    ENDC = '\033[0m'
    BOLD = '\033[1m'
    UNDERLINE = '\033[4m'

def format_question(question_dict):
    """Format a structured question dict into a string for the LLM.

    For open-ended questions, returns just the text.
    For MCQ questions, appends the numbered options.
    """
    text = question_dict["text"]
    q_type = question_dict.get("type", "open_ended")
    options = question_dict.get("options")

    if q_type == "mcq" and options:
        options_str = "\n".join(f"  {i+1}. {opt}" for i, opt in enumerate(options))
        return f"{text}\n[Multiple Choice - the user must pick one of these options]\nOptions:\n{options_str}"

    return text

def match_mcq_option(answer, options):
    """Try to match a free-text answer to one of the MCQ options.

    Uses case-insensitive substring matching. Returns the best-matching
    option text, or the original answer if no match is found.
    """
    answer_lower = answer.strip().lower()

    # Exact match (case-insensitive)
    for opt in options:
        if opt.lower() == answer_lower:
            return opt

    # Substring match: option text found in the answer
    for opt in options:
        if opt.lower() in answer_lower:
            return opt

    # Substring match: answer found in option text
    for opt in options:
        if answer_lower in opt.lower():
            return opt

    # No match found, return original answer
    return answer