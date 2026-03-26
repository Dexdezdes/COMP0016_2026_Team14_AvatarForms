import sys
sys.path.append("..\\Backend")  # Add Backend directory to sys.path to allow imports

import pytest
import asyncio
import json
import websockets
from unittest.mock import Mock, patch, AsyncMock, MagicMock

# Import the module to test
from sockets import (
    stream_message, 
    websocket_handler, 
    wait_for_browser_connection,
    start_server,
    connected_clients,
    browser_connected_event
)


@pytest.fixture
def clear_global_state():
    """Fixture to clear global state before each test"""
    connected_clients.clear()
    browser_connected_event.clear()
    yield
    connected_clients.clear()
    browser_connected_event.clear()


@pytest.mark.asyncio
async def test_stream_message_with_clients(clear_global_state):
    """Test streaming message to connected clients"""
    # Create mock clients
    mock_client1 = AsyncMock()
    mock_client2 = AsyncMock()
    
    connected_clients.add(mock_client1)
    connected_clients.add(mock_client2)
    
    await stream_message("test_type", "test content")
    
    # Verify both clients received the message
    expected_message = json.dumps({
        "type": "test_type",
        "content": "test content"
    })
    
    mock_client1.send.assert_called_once_with(expected_message)
    mock_client2.send.assert_called_once_with(expected_message)


@pytest.mark.asyncio
async def test_stream_message_no_clients(clear_global_state):
    """Test streaming message when no clients are connected"""
    # Should not raise any errors
    await stream_message("test_type", "test content")
    # No assertions needed - just ensure it doesn't crash


@pytest.mark.asyncio
async def test_stream_message_with_exceptions(clear_global_state):
    """Test streaming message when some clients raise exceptions"""
    mock_client1 = AsyncMock()
    mock_client2 = AsyncMock()
    mock_client2.send.side_effect = Exception("Connection error")
    
    connected_clients.add(mock_client1)
    connected_clients.add(mock_client2)
    
    # Should not raise exception
    await stream_message("test_type", "test content")
    
    # First client should have received message
    mock_client1.send.assert_called_once()
    # Second client attempted to send but failed - no exception propagated
    mock_client2.send.assert_called_once()


@pytest.mark.asyncio
async def test_wait_for_browser_connection(clear_global_state):
    """Test waiting for browser connection"""
    # Create a task that will set the event after a delay
    async def set_event_after_delay():
        await asyncio.sleep(0.1)
        browser_connected_event.set()
    
    task = asyncio.create_task(set_event_after_delay())
    await wait_for_browser_connection()
    
    assert browser_connected_event.is_set()
    await task


@pytest.mark.asyncio
async def test_wait_for_browser_connection_already_connected(clear_global_state):
    """Test waiting for browser when already connected"""
    browser_connected_event.set()
    await wait_for_browser_connection()
    assert browser_connected_event.is_set()

@pytest.mark.asyncio
async def test_multiple_clients_streaming(clear_global_state):
    """Test streaming to multiple clients simultaneously"""
    mock_clients = [AsyncMock() for _ in range(5)]
    for client in mock_clients:
        connected_clients.add(client)
    
    await stream_message("broadcast", "Hello all")
    
    # Verify all clients received the message
    expected_message = json.dumps({
        "type": "broadcast",
        "content": "Hello all"
    })
    
    for client in mock_clients:
        client.send.assert_called_once_with(expected_message)


@pytest.mark.asyncio
async def test_stream_message_empty_content(clear_global_state):
    """Test streaming message with empty content"""
    mock_client = AsyncMock()
    connected_clients.add(mock_client)
    
    await stream_message("empty", "")
    
    expected_message = json.dumps({
        "type": "empty",
        "content": ""
    })
    
    mock_client.send.assert_called_once_with(expected_message)
