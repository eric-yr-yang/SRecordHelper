using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SRecordHelper;

/// <summary>
/// 档案块
/// </summary>
public class SRecordBlock
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
                    RecordType = SRecordTypes.S1,
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
