from fastapi import FastAPI, HTTPException
from pydantic import BaseModel
from typing import List, Optional
import asyncio
import uvicorn
app = FastAPI()

# Global state to hold questionnaire data
questionnaire_data = None
questionnaire_ready_event = asyncio.Event()

class QuestionnaireRequest(BaseModel):
    description: str
    questions: List[str]

@app.post("/api/questionnaire/start")
async def receive_questionnaire(questionnaire: QuestionnaireRequest):
    """
    Endpoint to receive questionnaire data from C# application.
    Expected JSON format:
    {
        "description": "Context/description for the interview",
        "questions": ["Question 1?", "Question 2?", ...]
    }
    """
    global questionnaire_data

    try:
        # Validate the incoming data
        if not questionnaire.questions or len(questionnaire.questions) == 0:
            raise HTTPException(status_code=400, detail="Questions list cannot be empty")
    
        # Store questionnaire data in format expected by build_from_json
        questionnaire_data = {
            "description": questionnaire.description,
            "questions": questionnaire.questions
        }
    
        # Signal that questionnaire is ready
        questionnaire_ready_event.set()
    
        return {
            "status": "success",
            "message": f"Questionnaire received and ready for interview",
            "question_count": len(questionnaire.questions)
        }

    except Exception as e:
        print(f"Error receiving questionnaire: {str(e)}")
        raise HTTPException(status_code=500, detail=f"Error processing questionnaire: {str(e)}")

async def wait_for_questionnaire():
    print("Waiting for questionnaire data from C# application...")
    await questionnaire_ready_event.wait()
    return questionnaire_data

def reset_questionnaire():
    global questionnaire_data
    questionnaire_data = None
    questionnaire_ready_event.clear()

def run_http_api():
    uvicorn.run(app, host="0.0.0.0", port=8083, log_level="debug")
