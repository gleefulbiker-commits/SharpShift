using System.Collections.Generic;

namespace SharpShift.Inventory.Core.Utilities
{
    /// <summary>
    /// Catalog of common Visual Studio/.NET project system GUIDs.
    /// </summary>
    /// <remarks>
    /// Keys are human-readable project type labels and values are GUIDs in canonical brace format.
    ///
    /// This catalog includes both primary project type GUIDs (for example C# legacy, C# SDK-style,
    /// ASP.NET Web Application) and selected related flavor/system identifiers that can appear in
    /// legacy solution and project metadata.
    ///
    /// In modern SDK-style projects (.NET 5+ through .NET 10), explicit project type GUIDs are often
    /// not required in project files and may be absent in newer solution formats.
    /// </remarks>
    public static class DotNetProjectTypeGuids
    {
        /// <summary>
        /// Expanded mapping of known GUID identifiers to .NET/Visual Studio project type names.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, string> Map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"] = "C# (Legacy)",
            ["{9A19103F-16F7-4668-BE54-9A1E7A4F7556}"] = "C# (SDK-Style)",
            ["{F184B08F-C81C-45F6-A57F-5ABD9991F28F}"] = "VB.NET (Legacy)",
            ["{778DAE3C-4631-46EA-AA77-85C1314464D9}"] = "VB.NET (SDK-Style)",
            ["{F2A71F9B-5D33-465A-A702-920D77279786}"] = "F#",
            ["{BC8A1FFA-BEE3-4634-8014-F334798102B3}"] = "Visual C++",
            ["{C8A15946-EA74-4A3E-9AAE-EBBDFD2C7D3A}"] = "Visual C++ (Shared Items)",
            ["{349C5851-65DF-11DA-9384-00065B846F21}"] = "ASP.NET Web Application",
            ["{603C0E0B-DB56-11DC-BE95-000D561079B0}"] = "ASP.NET MVC 1-5",
            ["{8BB2217D-0F2D-49D1-97BC-3654ED321F3B}"] = "ASP.NET 5",
            ["{E24C65DC-7377-472B-9ABA-BC803B73C61A}"] = "ASP.NET Web Site",
            ["{3AC096D0-A1C2-E12C-1390-A8335801FDAB}"] = "MSTest Test Project (Legacy) / Unit Test Project (VS2010+)",
            ["{D954291E-2A0B-460D-934E-DC6B0785DB48}"] = "Shared Project",
            ["{786C830F-07A1-408B-BD7F-6EE04809D6DB}"] = "Portable Class Library (PCL)",
            ["{60DC8134-EBA5-43B8-BCC9-BB4BC16C2548}"] = "WPF Project Flavor",
            ["{BC8A1FFA-BEE3-4634-8014-F334798102D3}"] = "Windows Store App",
            ["{A5A43C5B-0F4C-4D42-94FC-5E8A1C7D7AAB}"] = "Universal Windows Platform (UWP)",
            ["{76F1466A-8B6D-4E39-A767-685A06062A39}"] = "Windows Phone 8/8.1",
            ["{A1591282-1198-4647-A2B1-27E5FF5F6F3B}"] = "Silverlight",
            ["{14822709-B5A1-4724-98CA-57A101D1B079}"] = "Workflow (C#)",
            ["{CB4CE8C6-1BDB-4DC7-A4D3-65A1999772F8}"] = "Workflow (VB.NET)",
            ["{3D9AD99F-2412-4246-B90B-4EAA41C64699}"] = "WCF",
            ["{F85E285D-A4E0-4152-9332-AB1D724D3325}"] = "Model-View-Controller v2",
            ["{E53F8FEA-EAE0-44A6-8774-FFD645390401}"] = "WCF Data Services",
            ["{54435603-DBB4-11D2-8724-00A0C9A8B90C}"] = "Deployment (Setup)",
            ["{00D1A9C2-B5F0-4AF3-8072-F6C62B433612}"] = "Database Project (SQL Server)",
            ["{C8D11400-126E-41CD-887F-60BD40844F9E}"] = "SQL Server Database Project (Legacy)",
            ["{593B0543-81F6-4436-BA1E-4747859CAAE2}"] = "SharePoint (C#)",
            ["{EC05E597-79D4-47F3-ADA0-324C4F7C7484}"] = "SharePoint (VB.NET)",
            ["{F8810EC1-6754-47FC-A15F-DFABD2E3FA90}"] = "SharePoint Workflow",
            ["{BAA0C2D2-18E2-41B9-852F-F413020CAA33}"] = "Visual Studio Tools for Office (VSTO)",
            ["{C1CDDADD-2546-481F-9697-4EA41081F2FC}"] = "Office/SharePoint App",
            ["{CC5FD16D-436D-48AD-A40C-5A424C6E3E79}"] = "Azure Cloud Service",
            ["{E2FF0EA2-4842-46E0-A434-C62C75BAEC67}"] = "Azure Resource Group",
            ["{E53339B2-1760-4266-BCC7-CA923CBCF16C}"] = "Docker Compose",
            ["{A07B5EB6-E848-4116-A8D0-A826331D98C6}"] = "Service Fabric Application",
            ["{F20D482A-C6B4-4A88-9F33-A5E8D5E94C42}"] = "Service Fabric Service",
            ["{EFBA0AD7-5A72-4C68-AF49-83D382785DCF}"] = "Xamarin.Android / Mono for Android",
            ["{6BC8ED88-2882-458C-8E55-DFD12B67127B}"] = "Xamarin.iOS / MonoTouch",
            ["{948B3504-5B70-4649-8F10-95E76C5F2642}"] = "Xamarin.Mac",
            ["{FEACFBD2-3405-455C-9665-78FE426C6842}"] = "Xamarin.Forms (Portable)",
            ["{9092AA53-FB77-4645-B42D-1CCCA6BD08BD}"] = "Node.js Tools for Visual Studio",
            ["{262852C6-CD72-467D-83FE-5EEB1973A190}"] = "JavaScript UWP",
            ["{888888A0-9F3D-457C-B088-3A5042F75D52}"] = "Python",
            ["{82B43B9B-A64C-4715-B499-D71E9CA2BD60}"] = "Visual Studio Extensibility (VSIX)",
            ["{66A26720-8FB5-11D2-AA7E-00C04F688DDE}"] = "Solution Folder"
        };
    }
}