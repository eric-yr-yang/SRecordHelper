using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace SRecordHelper;

/// <summary>
/// Motorola S-record 档案文件
/// </summary>
public class SRecordFile
{
    #region 定义
    /// <summary>
    /// 档案块
    /// </summary>
    public class RecordBlock
    {
        /// <summary>
        /// 起始地址
        /// </summary>
        public uint StartAddressValue { get; set; }

        /// <summary>
        /// 起始地址
        /// </summary>
        public byte[] StartAddress => BitConverter.GetBytes(StartAddressValue).Reverse().ToArray();

        /// <summary>
        /// 下一个内容地址
        /// </summary>
        public uint NextAddressValue => (uint)(StartAddressValue + Data.Count);

        /// <summary>
        /// 下一个内容地址
        /// </summary>
        public byte[] NextAddress => BitConverter.GetBytes(NextAddressValue).Reverse().ToArray();

        /// <summary>
        /// 结束地址
        /// </summary>
        public uint EndAddressValue => NextAddressValue - 1;

        /// <summary>
        /// 结束地址
        /// </summary>
        public byte[] EndAddress => BitConverter.GetBytes(EndAddressValue).Reverse().ToArray();

        /// <summary>
        /// 数据
        /// </summary>
        public List<byte> Data { get; set; } = [];

        /// <summary>
        /// 档案项目（仅包含 S1-S3 的记录）
        /// </summary>
        public List<SRecord> SRecords { get; } = [];

        /// <summary>
        /// 根据内容刷新 SRecord 对象
        /// </summary>
        /// <param name="dataLength"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        /// <exception cref="Exception"></exception>
        public List<SRecord> UpdateSRecords(int dataLength)
        {
            if (dataLength < 1 || dataLength > 250)
                throw new ArgumentOutOfRangeException(nameof(dataLength));

            SRecords.Clear();

            var firstAddress = StartAddressValue;  // 档案的起始地址
            var thisAddress = firstAddress;  // 档案块中正在处理的数据的地址
            var blockData = Data.ToArray();  // 块数据的快照
            
            var data = new List<byte>();  // 当前档案项的数据
            var c = blockData.Select((b, i) =>
            {
                data.Add(b);
                if ((i + 1) % dataLength == 0 || (i + 1) == blockData.Length)
                {
                    var s = new SRecord
                    {
                        RecordType = SRecord.RecordTypes.S1,
                        AddressValue = firstAddress,
                        Data = [.. data]
                    };
                    if (!s.MakeThisValid())
                        throw new Exception();
                    SRecords.Add(s);
                    data.Clear();
                    firstAddress = thisAddress + 1;
                }
                thisAddress++;
                return b;
            }).Count();  // 已经处理的字节数

            return SRecords;
        }
    }
    #endregion 定义

    public SRecordFile() { }

    /// <summary>
    /// 
    /// </summary>
    /// <remarks>
    /// 将逐行解析 S-record 条目，并进行验证，如果不符合规范将抛出异常。验证的内容包括：校验和、计数、结束符。<br/>
    /// 将地址连续的数据合并为档案块对象，档案块对象会按照 S-record 条目的先后顺序尝试根据地址对数据进行拼接。
    /// </remarks>
    /// <param name="lines">S-record 文件按行分割的数组</param>
    /// <exception cref="Exception"></exception>
    public SRecordFile(string[] lines)
    {
        SRecord.RecordTypes? recordType = null;  // 当前记录类型
        RecordBlock? recordBlock = null;  // 当前数据组
        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;  // 跳过空行
            SRecord sRecord = new(line);
            if (!sRecord.IsValid)
                throw new Exception("数据不符合规范！");

            switch (sRecord.RecordType)
            {
                case SRecord.RecordTypes.S0:
                    Title = Encoding.ASCII.GetString(sRecord.Data);
                    break;
                case SRecord.RecordTypes.S1:
                case SRecord.RecordTypes.S2:
                case SRecord.RecordTypes.S3:
                    recordType = sRecord.RecordType;
                    if (recordBlock == null || sRecord.AddressValue != recordBlock.NextAddressValue)  // 检查地址是否连续。
                    {
                        // 当前未设置档案块，或地址不连续：如果存在可拼接的档案块时进行拼接，否则创建新的档案块。
                        if (RecordBlocks.FirstOrDefault(b => b.NextAddressValue == sRecord.AddressValue) is RecordBlock b)  // 尝试进行拼接
                        {
                            recordBlock = b;
                        }
                        else  // 创建新的档案块
                        {
                            recordBlock = new()
                            {
                                StartAddressValue = sRecord.AddressValue
                            };
                            RecordBlocks.Add(recordBlock);
                        }
                    }
                    recordBlock.Data.AddRange(sRecord.Data);
                    recordBlock.SRecords.Add(sRecord);
                    break;
                case SRecord.RecordTypes.S4:
                    // 预留，忽略，不做任何处理
                    break;
                case SRecord.RecordTypes.S5:
                case SRecord.RecordTypes.S6:
                    // 对记录的数量进行验证
                    var count = BitConverter.ToUInt32((new byte[4]).Concat(sRecord.Data).TakeLast(4).Reverse().ToArray());
                    var actureCount = recordBlock?.SRecords.Count ?? 0;
                    if (count != actureCount)
                        throw new Exception($"记录标记数量 ({count}) 与实际数量 ({actureCount}) 不匹配！");
                    break;
                case SRecord.RecordTypes.S7:
                case SRecord.RecordTypes.S8:
                case SRecord.RecordTypes.S9:
                    // 对当前档案块进行验证
                    if ((recordType == SRecord.RecordTypes.S1 && sRecord.RecordType == SRecord.RecordTypes.S9) ||
                        (recordType == SRecord.RecordTypes.S2 && sRecord.RecordType == SRecord.RecordTypes.S8) ||
                        (recordType == SRecord.RecordTypes.S3 && sRecord.RecordType == SRecord.RecordTypes.S7))
                    {
                        if (recordBlock == null)
                        {
                            throw new Exception("没有档案块可进行终止操作！");
                        }
                        if (sRecord.AddressValue != 0 && sRecord.AddressValue != recordBlock.StartAddressValue)
                        {
                            if (RecordBlocks.FirstOrDefault(rb => rb.StartAddressValue == sRecord.AddressValue) == null)
                                throw new Exception("档案终止符标记的起始地址与实际起始地址不匹配！");
                            else
                                Debug.WriteLine("档案终止符的位置未与所属块位置连续。");
                        }
                        recordType = null;
                    }
                    else
                    {
                        throw new Exception("档案终止符不正确！");
                    }
                    break;
                default:
                    throw new Exception("不支持的记录类型！");
            }
        }
    }

    /// <summary>
    /// 标题
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// 所有档案块
    /// </summary>
    public List<RecordBlock> RecordBlocks { get; set; } = [];

    /// <summary>
    /// 转换为 ASCII 文本行
    /// </summary>
    /// <param name="dataLength">每行的数据长度（最小为 1，最大为 250）</param>
    /// <param name="countRecord">是否包含计数项（S5 或 S6）</param>
    /// <param name="endRecord">是否包含终止项</param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    /// <exception cref="Exception"></exception>
    public List<string> ToSRecordTextLines(int dataLength, bool countRecord = false, bool endRecord = false)
    {
        if (dataLength < 1 || dataLength > 250)
            throw new ArgumentOutOfRangeException(nameof(dataLength));

        var lines = new List<string>();
        if (Title != null)
        {
            var s = new SRecord()
            {
                RecordType = SRecord.RecordTypes.S0,
                Data = Encoding.ASCII.GetBytes(Title),
            };
            if (!s.MakeThisValid())
                throw new Exception();
            lines.Add(s.AsciiText);
        }

        foreach (var block in RecordBlocks)
        {
            var records = block.UpdateSRecords(dataLength);
            lines.AddRange(records.Select(r => r.AsciiText));

            if (countRecord)
            {
                var s = new SRecord
                {
                    RecordType = SRecord.RecordTypes.S5,
                    Data = BitConverter.GetBytes(records.Count).Reverse().ToArray()
                };
                if (!s.MakeThisValid())
                    throw new Exception();
                lines.Add(s.AsciiText);
            }
            
            if (endRecord)
            {
                var s = new SRecord
                {
                    RecordType = SRecord.RecordTypes.S9,
                    AddressValue = block.StartAddressValue
                };
                if (!s.MakeThisValid())
                    throw new Exception();
                lines.Add(s.AsciiText);
            }
        }

        return lines;
    }
}
