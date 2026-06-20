# Excel 样式 + 公式 + 透视表完整流

## 用户想要什么
有一个销售数据表，需要加样式、添公式、做透视表。

## 数据
先用 ClosedXML 创建一个基础 xlsx：
```
Region | Product | Sales
East   | A       | 100
East   | B       | 200
West   | A       | 150
```

## 做了什么
```powershell
# 1. 应用样式
echo '{"sheet":"Data","entries":[{"preset":"Finance"}]}' > style.json
nong excel style data.xlsx style.json -o styled.xlsx --json

# 2. 写公式
echo '{"sheet":"Data","entries":[{"cell":"D1","formula":"SUM(C2:C4)"}]}' > formula.json
nong excel formula styled.xlsx formula.json -o with-formula.xlsx --json

# 3. 创建透视表
echo '{"sheet":"Data","range":"A1:C4","pivotSheet":"Summary","rowLabels":["Region"],"columnLabels":["Product"],"values":[{"field":"Sales","summary":"sum"}]}' > pivot.json
nong excel pivot with-formula.xlsx pivot.json -o final.xlsx --json
```

## 结果
三个命令链式操作同一个 xlsx，最终得到带样式、公式和透视表的完整 workbook。

## 关键点
- style/formula/pivot 都是对已有 xlsx 文件操作，不创建新文件
- 操作顺序自由，可以只做其中一步
- pivot 需要 source sheet 有表头行作为字段名
