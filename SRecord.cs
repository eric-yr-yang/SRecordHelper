using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Microsoft.VisualBasic;

namespace SRecordHelper;

/// <summary>
/// Motorola S-record 格式档案
/// </summary>
public class SRecord
{
    #region 定义
    /// <summary>
    /// 档案类型附加信息特性
    /// </summary>
    /// <param name="text"></param>
    /// <param name="addressLength"></param>
    /// <param name="hasData">是否有数据字段</param>
    [AttributeUsage(AttributeTargets.Field)]
    public class SRecordInfoAttribute(string text, int addressLength, bool hasData = false) : Attribute
    {
        /// <summary>
        /// 档案类型的 ASCII 文本
        /// </summary>
        public string Name { get; } = text;

        /// <summary>
        /// 地址长度（字节数）
        /// </summary>
        public int AddressLength { get; set; } = addressLength;

        /// <summary>
        /// 是否有数据字段
        /// </summary>
        public bool HasData { get; set; } = hasData;
    }
    #endregion 定义

    #region 静态
    /// <summary>
    /// 通过记录类型获取名称
    /// </summary>
    /// <param name="recordType"></param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public static string GetRecordTypeName(SRecordTypes recordType)
    {
        if (recordType.GetType().GetField(recordType.ToString())!.GetCustomAttribute(typeof(SRecordInfoAttribute)) is SRecordInfoAttribute sRecordInfoAttribute)
            return sRecordInfoAttribute.Name;
        throw new Exception("缺少特性信息！");
    }

    /// <summary>
    /// 通过名称获取记录类型
    /// </summary>
    /// <param name="recordTypeName"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static SRecordTypes GetRecordTypeByName(string recordTypeName)
    {
        recordTypeName = recordTypeName.Trim().ToUpper();
        var recordType = typeof(SRecordTypes).GetFields().FirstOrDefault(field =>
        {
            if (field.GetCustomAttribute(typeof(SRecordInfoAttribute)) is SRecordInfoAttribute sRecordInfoAttribute)
            {
                return sRecordInfoAttribute.Name == recordTypeName;
            }
            return false;
        });
        if (recordType != null)
        {
            return (SRecordTypes)recordType.GetValue(null)!;
        }
        throw new ArgumentOutOfRangeException(nameof(recordTypeName));
    }

    /// <summary>
    /// 计算校验和
    /// </summary>
    /// <remarks>
    /// 对所有字节进行求和，取末尾 8 位，求反码。
    /// </remarks>
    /// <param name="data"></param>
    /// <returns></returns>
    public static byte CalculateCheckSum(byte[] data)
    {
        var sum = data.Sum(x => x);
        sum &= 0xFF;
        sum = 0xFF - sum;
        return (byte)sum;
    }

    /// <summary>
    /// 提取数据中的有效字节长度（移除前导 0）
    /// </summary>
    /// <param name="rawData"></param>
    /// <param name="newData"></param>
    /// <param name="minLength">最低保留字节数</param>
    /// <returns></returns>
    public static int ValidByteLength(byte[] rawData, out byte[] newData, int minLength = 2)
    {
        var data = Convert.ToHexString(rawData);
        while (data.StartsWith('0')) data = data[1..];
        var c = data.Length / 2;
        newData = rawData[^Math.Max(c, minLength)..];
        return newData.Length;
    }

    /// <summary>
    /// 根据地址获取合适的记录类型
    /// </summary>
    /// <param name="address"></param>
    /// <param name="newAddress"></param>
    /// <returns></returns>
    public static SRecordTypes GetSuitRecordTypeForAddress(byte[] address, out byte[] newAddress)
    {
        var addr = Convert.ToHexString(address);
        while (addr.StartsWith('0')) addr = addr[1..];
        var c = addr.Length / 2;
        if (c < 2)
        {
            newAddress = address[^2..];
            return SRecordTypes.S1;
        }
        else if (c < 3)
        {
            newAddress = address[^3..];
            return SRecordTypes.S2;
        }
        else
        {
            newAddress = address;
            return SRecordTypes.S3;
        }
    }
    #endregion 静态

    /// <summary>
    /// 
    /// </summary>
    public SRecord() { }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sRecordValue">一条 S-record 格式档案的文本</param>
    public SRecord(string sRecordValue)
    {
        sRecordValue = sRecordValue.Trim();  // 避免因为前后的空格产生影响
        RecordType = GetRecordTypeByName(sRecordValue[..2]);  // 取前 2 个字符（第 1 个字节）
        Length = Convert.ToByte(sRecordValue[2..4], 16);  // 取第 2 到第 4 个字符（第 2 个字节）
        Address = Convert.FromHexString(sRecordValue[4..(4 + AddressLength * 2)]);  // 从第 4 个字符开始，取地址长度的数据
        Data = Convert.FromHexString(sRecordValue[(4 + AddressLength * 2)..(4 + Length * 2 - 2)]);  // 从地址后，取到标记字节长度的末尾的前 2 个字符（留出校验和）
        CheckSum = Convert.ToByte(sRecordValue[(4 + Length * 2 - 2)..(4 + Length * 2)], 16);  // 取标记字节长度的末尾 2 个字符，如果后续有剩余则忽略
    }

    /// <summary>
    /// 档案类型
    /// </summary>
    public SRecordTypes RecordType { get; set; }

    /// <summary>
    /// 字节长度（地址 + 数据 + 校验和）
    /// </summary>
    /// <remarks>
    /// 通常最小的情况为：2 字节地址 + 1 字节校验和 = 3 字节，最大的情况为 0xFF。
    /// </remarks>
    public byte Length { get; set; }

    /// <summary>
    /// 计算的实际字节长度
    /// </summary>
    public byte CalculatedLength
    {
        get
        {
            var l = Address.Length + Data.Length + 1;
            if (l > byte.MaxValue)
                throw new Exception("长度超出容量上限！");
            return (byte)l;
        }
    }

    /// <summary>
    /// 地址
    /// </summary>
    public byte[] Address { get; set; } = [];

    /// <summary>
    /// 地址
    /// </summary>
    public uint AddressValue
    {
        get
        {
            var address = (new byte[4]).Concat(Address).TakeLast(4).Reverse().ToArray();
            return BitConverter.ToUInt32(address);
        }
        set
        {
            Address = BitConverter.GetBytes(value).Reverse().ToArray();
        }
    }

    /// <summary>
    /// 档案标记的地址长度
    /// </summary>
    public int AddressLength
    {
        get
        {
            if (RecordType.GetType().GetField(RecordType.ToString())!.GetCustomAttribute(typeof(SRecordInfoAttribute)) is not SRecordInfoAttribute sRecordInfoAttribute)
                throw new Exception("缺少特性信息！");
            return sRecordInfoAttribute.AddressLength;
        }
    }

    /// <summary>
    /// 数据
    /// </summary>
    public byte[] Data { get; set; } = [];

    /// <summary>
    /// 计算的实际的数据长度
    /// </summary>
    public int CalculatedDataLength => Length - AddressLength - 1;

    /// <summary>
    /// 校验和
    /// </summary>
    public byte CheckSum { get; set; }

    /// <summary>
    /// 计算的实际的校验和
    /// </summary>
    public byte CalculatedCheckSum
    {
        get
        {
            byte[] content = [Length, .. Address, .. Data];
            return CalculateCheckSum(content);
        }
    }

    /// <summary>
    /// 长度是否符合 S-record 格式
    /// </summary>
    public bool IsLengthValid => Length == CalculatedLength;

    /// <summary>
    /// 地址是否符合 S-record 格式
    /// </summary>
    public bool IsAddressValid
    {
        get
        {
            return Address.Length == AddressLength;
        }
    }

    /// <summary>
    /// 数据是否符合 S-record 格式
    /// </summary>
    public bool IsDataValid
    {
        get
        {
            if (RecordType.GetType().GetField(RecordType.ToString())!.GetCustomAttribute(typeof(SRecordInfoAttribute)) is not SRecordInfoAttribute sRecordInfoAttribute)
                throw new Exception("缺少特性信息！");
            return sRecordInfoAttribute.HasData || (!sRecordInfoAttribute.HasData && (Data.Length == 0));
        }
    }

    /// <summary>
    /// 校验和是否正确
    /// </summary>
    public bool IsCheckSumValid => CheckSum == CalculatedCheckSum;

    /// <summary>
    /// 是否符合 S-record 格式
    /// </summary>
    public bool IsValid => IsLengthValid && IsAddressValid && IsDataValid && IsCheckSumValid;

    /// <summary>
    /// 使该档案符合格式标准
    /// </summary>
    /// <remarks>
    /// 如果是 S1-S3，则根据地址的长度更新记录类型。
    /// 其他情况会重置地址信息。
    /// </remarks>
    /// <returns></returns>
    public bool MakeThisValid()
    {
        if (RecordType == SRecordTypes.S0 && AddressValue == 0)
        {
            Address = new byte[AddressLength];
        }
        else if (new[] { SRecordTypes.S1, SRecordTypes.S2, SRecordTypes.S3 }.Contains(RecordType))
        {
            RecordType = GetSuitRecordTypeForAddress(Address, out var newAddress);
            Address = newAddress;
        }
        else if (new[] { SRecordTypes.S7, SRecordTypes.S8, SRecordTypes.S9 }.Contains(RecordType))
        {
            _ = GetSuitRecordTypeForAddress(Address, out var newAddress);
            Address = newAddress;
            Data = [];
        }
        else if (new[] { SRecordTypes.S5, SRecordTypes.S6 }.Contains(RecordType))
        {
            Address = [];
            var l = ValidByteLength(Data, out var newData, minLength: 2);
            Data = newData;
            RecordType = l switch
            {
                2 => SRecordTypes.S5,
                3 => SRecordTypes.S6,
                _ => throw new Exception(),
            };
        }
        else
        {
            Address = [];
        }
        Length = CalculatedLength;
        CheckSum = CalculatedCheckSum;
        return IsValid;
    }

    /// <summary>
    /// ASCII 文本
    /// </summary>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public string AsciiText
    {
        get
        {
            if (!IsValid)
                throw new Exception("记录不符合规范，请检查！");

            var text = new StringBuilder();
            text.Append(GetRecordTypeName(RecordType));
            text.Append(Length.ToString("X2"));
            text.Append(Convert.ToHexString(Address));
            text.Append(Convert.ToHexString(Data));
            text.Append(CheckSum.ToString("X2"));

            return text.ToString();
        }
    }
}
