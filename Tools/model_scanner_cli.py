from openai import OpenAI
import json
import sys

def scan_models(api_key):
    client = OpenAI(api_key=api_key, base_url="https://api.siliconflow.cn/v1")
    
    results = []
    
    try:
        models = client.models.list()
        all_ids = sorted([m.id for m in models.data])
        
        # 为了性能和演示，我们只扫描前 50 个或者特定的典型模型
        # 或者干脆让用户选择扫描哪些
        # 这里我们先获取列表
        
        for m_id in all_ids:
            results.append({
                "id": m_id,
                "status": "pending",
                "message": ""
            })
            
    except Exception as e:
        print(json.dumps({"error": str(e)}))
        return

    print(json.dumps({"type": "list", "models": results}, ensure_ascii=False))

def test_model(api_key, model_id):
    client = OpenAI(api_key=api_key, base_url="https://api.siliconflow.cn/v1")
    try:
        response = client.chat.completions.create(
            model=model_id,
            messages=[{"role": "user", "content": "hi"}],
            max_tokens=5
        )
        content = response.choices[0].message.content.strip().replace("\n", " ")
        print(json.dumps({"type": "result", "id": model_id, "success": True, "message": content}, ensure_ascii=False))
    except Exception as e:
        err_str = str(e)
        msg = "未知错误"
        if "30001" in err_str:
            msg = "余额不足"
        elif "20012" in err_str:
            msg = "模型未上线"
        elif "30003" in err_str:
            msg = "模型受限"
        else:
            msg = err_str[:100]
        print(json.dumps({"type": "result", "id": model_id, "success": False, "message": msg}, ensure_ascii=False))

if __name__ == "__main__":
    if len(sys.argv) < 3:
        sys.exit(1)
        
    action = sys.argv[1]
    key = sys.argv[2]
    
    if action == "list":
        scan_models(key)
    elif action == "test" and len(sys.argv) >= 4:
        test_model(key, sys.argv[3])
