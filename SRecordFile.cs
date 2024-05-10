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
    /// <summary>
    /// 
    /// </summary>
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
        SRecordTypes? recordType = null;  // 当前记录类型
        SRecordBlock? recordBlock = null;  // 当前数据组
        var lineNumber = 0;  // 当前行号
        foreach (string line in lines)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line)) continue;  // 跳过空行
            SRecord sRecord = new(line);
            if (!sRecord.IsValid)
                throw new Exception($"[{lineNumber}] 数据不符合规范！");

            switch (sRecord.RecordType)
            {
                case SRecordTypes.S0:
                    Title = Encoding.ASCII.GetString(sRecord.Data);
                    break;
                case SRecordTypes.S1:
                case SRecordTypes.S2:
                case SRecordTypes.S3:
                    recordType = sRecord.RecordType;
                    if (recordBlock == null || sRecord.AddressValue != recordBlock.NextAddressValue)  // 检查地址是否连续。
                    {
                        // 当前未设置档案块，或地址不连续：如果存在可拼接的档案块时进行拼接，否则创建新的档案块。
                        if (SRecordBlocks.FirstOrDefault(b => b.NextAddressValue == sRecord.AddressValue) is SRecordBlock b)  // 尝试进行拼接
                        {
                            recordBlock = b;
                        }
                        else  // 创建新的档案块
                        {
                            recordBlock = new()
                            {
                                StartAddressValue = sRecord.AddressValue
                            };
                            SRecordBlocks.Add(recordBlock);
                        }
                    }
                    recordBlock.Data.AddRange(sRecord.Data);
                    recordBlock.SRecords.Add(sRecord);
                    break;
                case SRecordTypes.S4:
                    // 预留，忽略，不做任何处理
                    break;
                case SRecordTypes.S5:
                case SRecordTypes.S6:
                    // 对记录的数量进行验证
                    var count = BitConverter.ToUInt32((new byte[4]).Concat(sRecord.Data).TakeLast(4).Reverse().ToArray());
                    var actureCount = recordBlock?.SRecords.Count ?? 0;
                    if (count != actureCount)
                        throw new Exception($"[{lineNumber}] 记录标记数量 ({count}) 与实际数量 ({actureCount}) 不匹配！");
                    break;
                case SRecordTypes.S7:
                case SRecordTypes.S8:
                case SRecordTypes.S9:
                    // 对当前档案块进行验证
                    if ((recordType == SRecordTypes.S1 && sRecord.RecordType == SRecordTypes.S9) ||
                        (recordType == SRecordTypes.S2 && sRecord.RecordType == SRecordTypes.S8) ||
                        (recordType == SRecordTypes.S3 && sRecord.RecordType == SRecordTypes.S7))
                    {
                        if (recordBlock == null)
                        {
                            throw new Exception($"[{lineNumber}] 没有档案块可进行终止操作！");
                        }
                        if (sRecord.AddressValue != 0 && sRecord.AddressValue != recordBlock.StartAddressValue)
                        {
                            if (SRecordBlocks.FirstOrDefault(rb => rb.StartAddressValue == sRecord.AddressValue) == null)
                                throw new Exception($"[{lineNumber}] 档案终止符标记的起始地址与实际起始地址不匹配！");
                            else
                                Debug.WriteLine($"[{lineNumber}] 档案终止符的位置未与所属块位置连续。");
                        }
                        recordType = null;
                    }
                    else
                    {
                        throw new Exception($"[{lineNumber}] 档案终止符不正确！");
                    }
                    break;
                default:
                    throw new Exception($"[{lineNumber}] 不支持的记录类型！");
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
    public List<SRecordBlock> SRecordBlocks { get; set; } = [];

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
                RecordType = SRecordTypes.S0,
                Data = Encoding.ASCII.GetBytes(Title),
            };
            if (!s.MakeThisValid())
                throw new Exception();
            lines.Add(s.AsciiText);
        }

        foreach (var block in SRecordBlocks)
        {
            var records = block.UpdateSRecords(dataLength);
            lines.AddRange(records.Select(r => r.AsciiText));

            if (countRecord)
            {
                var s = new SRecord
                {
                    RecordType = SRecordTypes.S5,
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
                    RecordType = SRecordTypes.S9,
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
