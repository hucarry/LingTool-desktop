import tkinter as tk
from tkinter import ttk, messagebox
from openai import OpenAI
import threading

class ModelTesterGUI:
    def __init__(self, root):
        self.root = root
        self.root.title("SiliconFlow 全模型可用性自动扫描器")
        self.root.geometry("900x650")

        # API 配置
        self.api_key = "sk-kkxygfpzmzgfnzfuahierxuxfhcstxfbbtjqimkmjsnzlgme"
        self.base_url = "https://api.siliconflow.cn/v1"
        self.client = OpenAI(api_key=self.api_key, base_url=self.base_url)

        self.create_widgets()

    def create_widgets(self):
        # 顶部面板
        top_frame = tk.Frame(self.root, pady=10)
        top_frame.pack(fill=tk.X)

        tk.Label(top_frame, text="SiliconFlow 模型可用性扫描", font=("微软雅黑", 12, "bold")).pack(side=tk.LEFT, padx=20)
        
        self.btn_fetch = tk.Button(top_frame, text="第一步：获取全量模型列表", command=self.start_fetch_models, 
                                   bg="#FF9800", fg="white", font=("微软雅黑", 10), padx=10)
        self.btn_fetch.pack(side=tk.LEFT, padx=10)

        self.btn_test = tk.Button(top_frame, text="第二步：开始批量测试可用性", command=self.start_testing, 
                                  bg="#2196F3", fg="white", font=("微软雅黑", 10), padx=15, state=tk.DISABLED)
        self.btn_test.pack(side=tk.LEFT, padx=10)

        # 列表区域
        columns = ("model_id", "status", "message")
        self.tree = ttk.Treeview(self.root, columns=columns, show="headings")
        self.tree.heading("model_id", text="模型 ID (双击复制)")
        self.tree.heading("status", text="状态")
        self.tree.heading("message", text="详细信息 / 回复内容")

        self.tree.column("model_id", width=350)
        self.tree.column("status", width=120, anchor=tk.CENTER)
        self.tree.column("message", width=400)

        # 滚动条
        scrollbar = ttk.Scrollbar(self.root, orient=tk.VERTICAL, command=self.tree.yview)
        self.tree.configure(yscroll=scrollbar.set)
        self.tree.pack(fill=tk.BOTH, expand=True, padx=20, pady=10)
        scrollbar.pack(side=tk.RIGHT, fill=tk.Y)

        # 绑定双击事件，方便复制模型名字
        self.tree.bind("<Double-1>", self.on_double_click)

        # 样式
        self.tree.tag_configure('success', foreground='green')
        self.tree.tag_configure('fail', foreground='red')
        self.tree.tag_configure('wait', foreground='gray')

        # 底部状态栏
        self.status_var = tk.StringVar(value="请点击『获取全量模型列表』开始")
        status_bar = tk.Label(self.root, textvariable=self.status_var, bd=1, relief=tk.SUNKEN, anchor=tk.W)
        status_bar.pack(side=tk.BOTTOM, fill=tk.X)

    def on_double_click(self, event):
        item = self.tree.selection()[0]
        model_id = self.tree.item(item)['values'][0]
        self.root.clipboard_clear()
        self.root.clipboard_append(model_id)
        self.status_var.set(f"已复制模型 ID: {model_id}")

    def start_fetch_models(self):
        self.btn_fetch.config(state=tk.DISABLED)
        self.status_var.set("正在从服务器下载模型列表...")
        # 清空
        for item in self.tree.get_children():
            self.tree.delete(item)
        
        threading.Thread(target=self.fetch_models_thread).start()

    def fetch_models_thread(self):
        try:
            models = self.client.models.list()
            # 排序一下，让结果更整齐
            sorted_models = sorted([m.id for m in models.data])
            
            for m_id in sorted_models:
                self.root.after(0, lambda mid=m_id: self.tree.insert("", tk.END, values=(mid, "就绪", "-"), tags=('wait')))
            
            self.root.after(0, lambda: self.status_var.set(f"成功获取 {len(sorted_models)} 个模型。准备开始测试。"))
            self.root.after(0, lambda: self.btn_test.config(state=tk.NORMAL))
        except Exception as e:
            self.root.after(0, lambda: messagebox.showerror("错误", f"获取列表失败: {e}"))
        finally:
            self.root.after(0, lambda: self.btn_fetch.config(state=tk.NORMAL))

    def start_testing(self):
        self.btn_test.config(state=tk.DISABLED)
        self.btn_fetch.config(state=tk.DISABLED)
        threading.Thread(target=self.run_tests).start()

    def run_tests(self):
        items = self.tree.get_children()
        total = len(items)
        for i, item in enumerate(items):
            model = self.tree.item(item)['values'][0]
            self.status_var.set(f"正在测试 ({i+1}/{total}): {model}")
            
            try:
                # 极简调用
                response = self.client.chat.completions.create(
                    model=model,
                    messages=[{"role": "user", "content": "hi"}],
                    max_tokens=5
                )
                answer = response.choices[0].message.content.strip().replace("\n", " ")
                self.root.after(0, self.update_item, item, "✅ 可用", answer, 'success')
            except Exception as e:
                err_str = str(e)
                status = "❌ 失败"
                if "30001" in err_str:
                    msg = "余额不足 (403)"
                elif "20012" in err_str:
                    msg = "模型未上线 (400)"
                elif "30003" in err_str:
                    msg = "模型已禁用/受限"
                else:
                    msg = err_str[:100]
                self.root.after(0, self.update_item, item, status, msg, 'fail')

        self.status_var.set(f"全部 {total} 个模型测试完成。")
        self.root.after(0, lambda: self.btn_test.config(state=tk.NORMAL))
        self.root.after(0, lambda: self.btn_fetch.config(state=tk.NORMAL))
        self.root.after(0, lambda: messagebox.showinfo("完成", "全量模型可用性扫描已结束！"))

    def update_item(self, item, status, msg, tag):
        model = self.tree.item(item)['values'][0]
        self.tree.item(item, values=(model, status, msg), tags=(tag))

if __name__ == "__main__":
    root = tk.Tk()
    app = ModelTesterGUI(root)
    root.mainloop()
