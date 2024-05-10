using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using static SRecordHelper.SRecord;

namespace SRecordHelper;

/// <summary>
/// 档案类型
/// </summary>
public enum SRecordTypes : byte
{
    /// <summary>
    /// 标题类型
    /// </summary>
    [SRecordInfo("S0", 2, true)]
    S0 = 0,

    /// <summary>
    /// 16 位地址档案
    /// </summary>
    [SRecordInfo("S1", 2, true)]
    S1 = 1,

    /// <summary>
    /// 24 位地址档案
    /// </summary>
    [SRecordInfo("S2", 3, true)]
    S2 = 2,

    /// <summary>
    /// 32 位地址档案
    /// </summary>
    [SRecordInfo("S3", 4, true)]
    S3 = 3,

    /// <summary>
    /// 保留
    /// </summary>
    [SRecordInfo("S4", 0)]
    S4 = 4,

    /// <summary>
    /// 2 字节计数
    /// </summary>
    [SRecordInfo("S5", 0, true)]
    S5 = 5,

    /// <summary>
    /// 3 字节计数
    /// </summary>
    [SRecordInfo("S6", 0, true)]
    S6 = 6,

    /// <summary>
    /// 32 位地址档案终止符
    /// </summary>
    [SRecordInfo("S7", 4)]
    S7 = 7,

    /// <summary>
    /// 24 位地址档案终止符
    /// </summary>
    [SRecordInfo("S8", 3)]
    S8 = 8,

    /// <summary>
    /// 16 位地址档案终止符
    /// </summary>
    [SRecordInfo("S9", 2)]
    S9 = 9
}
