import sys
sys.path.append("..\\Backend")  # Add Backend directory to sys.path to allow imports

import pytest
import os
import json
import tempfile

from tracing import LLM_Trace, Tracer


class TestLLMTrace:
    def test_llm_trace_initialization(self):
        """Test LLM_Trace initialization with all parameters"""
        trace = LLM_Trace(
            agent_name="test_agent",
            prompt="Test prompt",
            output="Test output",
            parameters={"temperature": 0.7, "max_tokens": 2048},
            response_time=1.23
        )
        
        assert trace.agent_name == "test_agent"
        assert trace.prompt == "Test prompt"
        assert trace.output == "Test output"
        assert trace.parameters == {"temperature": 0.7, "max_tokens": 2048}
        assert trace.response_time == 1.23

    def test_to_dict(self):
        """Test to_dict method returns correct dictionary"""
        trace = LLM_Trace(
            agent_name="test_agent",
            prompt="Test prompt",
            output="Test output",
            parameters={"temperature": 0.7, "max_tokens": 2048},
            response_time=2.5
        )
        
        result = trace.to_dict()
        
        assert result == {
            "agent": "test_agent",
            "prompt": "Test prompt",
            "output": "Test output",
            "parameters": {"temperature": 0.7, "max_tokens": 2048},
            "response_time": 2.5
        }

class TestTracer:
    def test_tracer_initialization(self):
        """Test Tracer initializations"""
        with tempfile.TemporaryDirectory() as tmpdir:
            custom_log_name = "custom_trace.json"
            model_params = {"temperature": 0.5, "max_tokens": 2048}
            
            tracer = Tracer(
                log_dir=tmpdir,
                log_name=custom_log_name,
                print_logs=True,
                model_params=model_params
            )
            
            assert tracer.log_dir == tmpdir
            assert tracer.logs == []
            assert tracer.print_logs is True
            assert tracer.model_params == model_params
            assert tracer.log_path == os.path.join(tmpdir, custom_log_name)
            assert os.path.exists(tracer.log_path)

    def test_tracer_creates_directory(self):
        """Test Tracer creates log directory if it doesn't exist"""
        with tempfile.TemporaryDirectory() as tmpdir:
            new_dir = os.path.join(tmpdir, "new_logs")
            tracer = Tracer(log_dir=new_dir)
            
            assert os.path.exists(new_dir)
            assert tracer.log_dir == new_dir

    @pytest.mark.parametrize("temperature", [None, 0.3])
    @pytest.mark.parametrize("print_logs", [True, False])
    def test_tracer_log(self, temperature, print_logs, capsys):
        """Test logging with (print_logs=True)"""
        with tempfile.TemporaryDirectory() as tmpdir:
            tracer = Tracer(log_dir=tmpdir, print_logs=print_logs)
            
            tracer.log(
                agent_name="test_agent",
                prompt="Test prompt",
                output="Test output",
                response_time=1.5,
                temperature=temperature
            )
            
            # Check that the expected output was printed
            if print_logs:
                captured = capsys.readouterr()
                assert "Agent: test_agent" in captured.out
                assert "Input: Test prompt" in captured.out
                assert "Output: Test output" in captured.out
                assert "Response Time: 1.50 seconds" in captured.out
            else:
                captured = capsys.readouterr()
                assert captured.out == ""
                
            assert os.path.exists(tracer.log_path)
            with open(tracer.log_path, 'r') as f:
                lines = f.readlines()
                assert len(lines) == 1
                log_entry = json.loads(lines[0])
                assert log_entry["agent"] == "test_agent"
                assert log_entry["prompt"] == "Test prompt"
                assert log_entry["output"] == "Test output"
                assert log_entry["response_time"] == 1.5