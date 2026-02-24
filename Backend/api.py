from flask import Flask, request, jsonify
import threading
import asyncio
import time
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


def start_http_server(port=8882):
    def run_flask():
        app.run(host='0.0.0.0', port=port, debug=False, use_reloader=False)

    thread = threading.Thread(target=run_flask, daemon=True)
    thread.start()
    print(f"{bcolors.OKGREEN}HTTP server started on http://localhost:{port}{bcolors.ENDC}")
    return thread