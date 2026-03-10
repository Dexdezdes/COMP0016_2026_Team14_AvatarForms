from flask import Flask, request, jsonify
import threading
import asyncio
import time
import requests
from formatting import bcolors

questionnaire_data = None
questionnaire_received_event = threading.Event()

app = Flask(__name__)
app.config['JSON_SORT_KEYS'] = False

@app.route('/questionnaire', methods=['POST'])
def receive_questionnaire():
    global questionnaire_data

    try:
        data = request.get_json()

        if not data:
            return jsonify({"error": "No JSON data provided"}), 400

        if "questions" not in data or "description" not in data:
            return jsonify({"error": "Missing 'questions' or 'description' in payload"}), 400

        questionnaire_data = data
        questionnaire_received_event.set()

        print(f"{bcolors.OKGREEN}Received questionnaire with {len(data['questions'])} questions{bcolors.ENDC}")
        print(f"{bcolors.OKCYAN}Description: {data['description']}{bcolors.ENDC}")

        return jsonify({"status": "success", "message": "Questionnaire received"}), 200

    except Exception as e:
        print(f"{bcolors.FAIL}Error receiving questionnaire: {e}{bcolors.ENDC}")
        return jsonify({"error": str(e)}), 500


async def wait_for_questionnaire():
    print(f"{bcolors.OKCYAN}Waiting for questionnaire data from frontend...{bcolors.ENDC}")
    await asyncio.get_event_loop().run_in_executor(None, questionnaire_received_event.wait)
    return questionnaire_data


def send_response(questionnaire_id, question_order, question, answer, port):
    url = f"http://localhost:{port}/response"

    payload = {
        "questionnaire_id": questionnaire_id,
        "question_order": question_order,
        "question": question,
        "answer": answer
    }

    try:
        response = requests.post(url, json=payload, timeout=5)

        if response.status_code == 200:
            print(f"{bcolors.OKGREEN}Successfully sent response for question {question_order} to C# backend{bcolors.ENDC}")
            return True
        else:
            print(f"{bcolors.FAIL}Failed to send response. Status: {response.status_code}, Error: {response.text}{bcolors.ENDC}")
            return False

    except requests.exceptions.ConnectionError:
        print(f"{bcolors.FAIL}Could not connect to C# ResponseAPIService at {url}. Make sure the service is running.{bcolors.ENDC}")
        return False
    except requests.exceptions.Timeout:
        print(f"{bcolors.FAIL}Timeout sending response to C# backend{bcolors.ENDC}")
        return False
    except Exception as e:
        print(f"{bcolors.FAIL}Error sending response to C# backend: {e}{bcolors.ENDC}")
        return False


def start_http_server(port):
    def run_flask():
        app.run(host='0.0.0.0', port=port, debug=False, use_reloader=False)

    thread = threading.Thread(target=run_flask, daemon=True)
    thread.start()
    print(f"{bcolors.OKGREEN}HTTP server started on http://localhost:{port}{bcolors.ENDC}")
    return thread