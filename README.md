# .s19，.s37 后缀文件的解析方法库

该后缀文件通常是 Motorola S-record 文件，其内容是记录地址和对应的字节数据。



## SRecordHelper 库

使用 .NET 8.0 框架。

- class SRecord
  - 概述：表示 S-record 文件中的单条记录文本。
  - 构造：
    - 使用 S-record 文件中的单条记录文本进行构造，在构造时提取所有有效信息。
  - 成员：
    - 解析 S-record 行信息的详细内容，包括：
      - 记录类型；
      - 字节长度；
      - 内存地址；
      - 数据内容；
      - 校验码。
    - 附加额外的属性，包括：
      - 计算实际的字节长度；
      - 计算实际的校验码。
- class SRecordFile
  - 概述：表示完整的 S-record 格式文件。
  - 构造：
    - 使用 S-record 格式文件的所有行数据进行构造，在构造时提取所有有效信息。

  - 成员：
    - 标题
    - 所有档案块：

- class SRecordBlock
  - 概述：具有起始地址的，一段地址连续的数据。




## 参考用例

```C#
string sample = """
    S00F000068656C6C6F202020202000003C
    S11F00007C0802A6900100049421FFF07C6C1B787C8C23783C6000003863000026
    S11F001C4BFFFFE5398000007D83637880010014382100107C0803A64E800020E9
    S111003848656C6C6F20776F726C642E0A0042
    S5030003F9
    S9030000FC
    """;  // 示例文件
// string sample = File.ReadAllText(@"ECU.s37");  // 读取文件
string[] lines = sample.Replace("\r\n", "\n").Split("\n");  // 按照换行符分割为行序列

SRecordFile sRecordFile = new SRecordFile(lines);  // 使用行序列创建 SRecordFile 对象（构造时会进行验证，验证不通过时会抛出异常）
SRecordBlock sRecordBlock = sRecordFile.SRecordBlocks[1];  // 获取第一个档案块对象
uint startAddress = sRecordBlock.StartAddressValue;  // 档案块对象的地址
byte[] data = sRecordBlock.Data;  // 获取档案块数据

var newLines = sRecordFile.ToSRecordTextLines(0x20);  // 从 SRecordFile 对象导出行序列

var newSRecordFile = new SRecordFile([.. newLines]);  // 使用导出的行序列重新创建 SRecordFile 对象验证比对
```



参考资料

- [SREC - 维基百科，自由的百科全书 (wikipedia.org)](https://zh.wikipedia.org/wiki/SREC)

