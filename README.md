# .s19，.s37 后缀文件的解析方法库

该后缀文件通常是 Motorola S-record 文件，其内容是记录地址和对应的字节数据。



## SRecordHelper 库

SRecordHelper 命名空间下，有 2 个公共类型：

- SRecord
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
- SRecordFile





参考资料

- [SREC - 维基百科，自由的百科全书 (wikipedia.org)](https://zh.wikipedia.org/wiki/SREC)

