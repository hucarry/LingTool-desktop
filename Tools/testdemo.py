print("请输入一个数字：")
num = input()
try:
	num = int(num)
	print("你输入的数字是：", num)
except ValueError:
	print("输入无效，请输入一个数字。")
