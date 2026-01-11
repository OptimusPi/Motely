# MCP Server Review and Fix Plan

## Current Status

**Location**: `Motely.API/McpProtocol/McpServer.cs`

**Status**: ⚠️ Needs review and testing

## Issues Identified

### 1. JSON-RPC 2.0 Compliance
- ✅ Request/Response structure looks correct
- ✅ Error handling present
- ⚠️ Need to verify all required fields are present
- ⚠️ Need to test with Claude Desktop

### 2. Protocol Version
- ✅ Using correct version: `2024-11-05`
- ✅ Capabilities properly defined

### 3. Tool Definitions
- ✅ Tools are properly defined
- ✅ Input schemas are complete
- ⚠️ Need to verify tool execution works correctly

### 4. Error Handling
- ✅ Try-catch blocks present
- ✅ Proper error codes (-32601, -32603)
- ⚠️ Need to add more specific error handling

## Testing Checklist

### Claude Desktop Integration
1. [ ] Test `initialize` handshake
2. [ ] Test `tools/list` returns all tools
3. [ ] Test `tools/call` for `generate_jaml_filter`
4. [ ] Test `tools/call` for `search_seeds`
5. [ ] Test `tools/call` for `get_search_status`
6. [ ] Test `tools/call` for `analyze_seed`
7. [ ] Test `tools/call` for `verify_seed`
8. [ ] Test error responses for invalid requests

### JSON-RPC 2.0 Compliance
1. [ ] Verify request format (id, method, params)
2. [ ] Verify response format (id, result/error)
3. [ ] Verify error format (code, message, data)
4. [ ] Test batch requests (if supported)
5. [ ] Test notification requests (no id)

## Recommended Fixes

### 1. Add Request Validation
```csharp
private void ValidateJsonRpcRequest(JsonRpcRequest request)
{
    if (request == null)
        throw new ArgumentNullException(nameof(request));
    
    if (string.IsNullOrWhiteSpace(request.Method))
        throw new ArgumentException("Method is required", nameof(request));
    
    // id can be null for notifications, but should be present for requests
    // (we'll handle both cases)
}
```

### 2. Improve Error Messages
```csharp
catch (Exception ex)
{
    _logger.LogError(ex, $"Error handling MCP request: {request.Method}");
    
    // Provide more detailed error information
    var errorData = new
    {
        method = request.Method,
        exceptionType = ex.GetType().Name,
        message = ex.Message,
        stackTrace = ex.StackTrace
    };
    
    return JsonRpcResponse.Error(request.Id, -32603, $"Internal error: {ex.Message}", errorData);
}
```

### 3. Add Logging
```csharp
_logger.LogDebug("MCP request received: {Method}, ID: {Id}", request.Method, request.Id);
```

### 4. Test stdio Mode
- Verify `McpStdioServer` works correctly
- Test with Claude Desktop stdio transport

## Implementation Priority

1. **High**: Test with Claude Desktop
2. **High**: Fix any JSON-RPC compliance issues
3. **Medium**: Improve error messages
4. **Medium**: Add request validation
5. **Low**: Add more logging

## Next Steps

1. Test MCP Server with Claude Desktop
2. Fix any issues found during testing
3. Document any limitations
4. Create example Claude Desktop configuration
