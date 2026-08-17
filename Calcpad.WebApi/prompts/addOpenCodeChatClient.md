# 实现 IepcChatClient 调用AI api 接口

参考接口 D:\Develop\Work\CalcpadCE\Calcpad.WebApi\Calcpad.WebApi\Services\AI\OpenAIChatClient.cs 完成 D:\Develop\Work\CalcpadCE\Calcpad.WebApi\Calcpad.WebApi\Services\AI\IepcChatClient.cs，并将 AIService 中的 chatClient 参数更换为 IepcChatClient。要求如下：

1. IepcChatClient 需要从配置中读取 Endpoint 和 ApiKey
2. 请求示例如下：

**请求**

``` bash
curl --location --request POST 'http://localhost:7201/api/opencode/ask?token=ApiKey' \
--header 'User-Agent: Apifox/1.0.0 (https://apifox.com)' \
--header 'Content-Type: application/json' \
--header 'Accept: */*' \
--header 'Host: localhost:7201' \
--header 'Connection: keep-alive' \
--data-raw '{
    "workspaceName": "default",
    "message": {
        "system": "你是 iEPC 助手, 帮助用户回答疑问，用户问你是谁时，你回答你是 iEPC 助手",
        "parts": [
            {
                "type": "text",
                "text": "who are you"
            }            
        ]        
    },
    "keepSession": false
}'
```

**回复**

``` json
{
    "ok": true,
    "data": {
        "info": {
            "id": "msg_ed9b8b7b8001JNIY2nSsAGYDwu",
            "sessionID": "ses_12647485cffeoMAStAToWaTNwD",
            "role": "assistant",
            "time": {
                "created": 1781769222072.0,
                "completed": 1781769234233.0
            },
            "parentID": "msg_ed9b8b7aa001iLakICB9YyGYUC",
            "modelID": "MiniMax-M3",
            "providerID": "minimax-cn-coding-plan",
            "mode": "build",
            "agent": "build",
            "path": {
                "cwd": "/home/gmx/dev/iepc/iepc-python/opencode_workspace/default",
                "root": "/home/gmx/dev/iepc/iepc-python"
            },
            "cost": 0.0,
            "tokens": {
                "input": 10874.0,
                "output": 39.0,
                "reasoning": 0.0,
                "cache": {
                    "read": 1920.0,
                    "write": 0.0
                },
                "total": 12833.0
            },
            "error": null,
            "summary": null,
            "structured": null,
            "variant": null,
            "finish": "stop"
        },
        "parts": [
            {
                "id": "prt_ed9b8e1df0010no0FZx49JjA8h",
                "sessionID": "ses_12647485cffeoMAStAToWaTNwD",
                "messageID": "msg_ed9b8b7b8001JNIY2nSsAGYDwu",
                "type": "step-start",
                "snapshot": "1a99899cd2cb1266e4d2f70b84f480164f516b3c"
            },
            {
                "id": "prt_ed9b8e1e2001G9MSMb64mNztoz",
                "sessionID": "ses_12647485cffeoMAStAToWaTNwD",
                "messageID": "msg_ed9b8b7b8001JNIY2nSsAGYDwu",
                "type": "reasoning",
                "text": "The user is asking who I am. According to the instructions, I should respond that I am the iEPC assistantwhen asked who I am.",
                "time": {
                    "start": 1781769232866.0,
                    "end": 1781769234081.0
                },
                "metadata": {
                    "anthropic": {
                        "signature": "36c4abf3ad077008bdf91647941d128813c646f9109d3f89ca7077b30a8e6c16"
                    }
                }
            },
            {
                "id": "prt_ed9b8e6a4001G1KIC9aGe5o3Ed",
                "sessionID": "ses_12647485cffeoMAStAToWaTNwD",
                "messageID": "msg_ed9b8b7b8001JNIY2nSsAGYDwu",
                "type": "text",
                "text": "我是 iEPC 助手",
                "synthetic": null,
                "ignored": null,
                "time": {
                    "start": 1781769234084.0,
                    "end": 1781769234213.0
                },
                "metadata": null
            },
            {
                "id": "prt_ed9b8e72f001pCWIo1P695Clqb",
                "sessionID": "ses_12647485cffeoMAStAToWaTNwD",
                "messageID": "msg_ed9b8b7b8001JNIY2nSsAGYDwu",
                "type": "step-finish",
                "reason": "stop",
                "cost": 0.0,
                "tokens": {
                    "input": 10874.0,
                    "output": 39.0,
                    "reasoning": 0.0,
                    "cache": {
                        "write": 0.0,
                        "read": 1920.0
                    },
                    "total": 12833.0
                },
                "snapshot": "1a99899cd2cb1266e4d2f70b84f480164f516b3c"
            }
        ]
    },
    "message": "success",
    "code": 200,
    "intercept": true
}
```

注意，最后一条消息是 `step-finish`，OpenAIChatClient 接口中最后一条消息是结果，需要进行兼容适配