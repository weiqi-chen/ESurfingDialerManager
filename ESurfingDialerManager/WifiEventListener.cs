using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ESurfingDialerManager;
public class WifiEventListener
{
    // -------------------- P/Invoke --------------------
    [DllImport("wlanapi.dll", SetLastError = true)]
    static extern uint WlanOpenHandle(
        uint dwClientVersion,
        IntPtr pReserved,
        out uint pdwNegotiatedVersion,
        out IntPtr phClientHandle
    );

    [DllImport("wlanapi.dll", SetLastError = true)]
    static extern uint WlanRegisterNotification(
        IntPtr hClientHandle,
        uint dwNotifSource,
        bool bIgnoreDuplicate,
        IntPtr funcCallback,
        IntPtr pContext,
        IntPtr pReserved,
        out uint pdwPrevNotifSource
    );

    [DllImport("wlanapi.dll", SetLastError = true)]
    static extern uint WlanCloseHandle(
        IntPtr hClientHandle,
        IntPtr pReserved
    );


    // -------------------- 常量 --------------------
    const uint WLAN_NOTIFICATION_SOURCE_ACM = 0x00000008; // 连接/断开事件
    const uint WLAN_CLIENT_VERSION_XP_SP2 = 1;
    const uint WLAN_CLIENT_VERSION_LONGHORN = 2;

    [Flags]
    public enum WLAN_NOTIFICATION_SOURCE : uint
    {
        WLAN_NOTIFICATION_SOURCE_NONE = 0x00000000, // 未知源
        WLAN_NOTIFICATION_SOURCE_ONEX = 0x00000004, // 802.1X 模块
        WLAN_NOTIFICATION_SOURCE_ACM = 0x00000008, // 自动配置模块
        WLAN_NOTIFICATION_SOURCE_MSM = 0x00000010, // 媒体特定模块
        WLAN_NOTIFICATION_SOURCE_SECURITY = 0x00000020, // 安全模块
        WLAN_NOTIFICATION_SOURCE_IHV = 0x00000040, // 独立硬件供应商(IHV)
        WLAN_NOTIFICATION_SOURCE_HNWK = 0x00000080, // 托管网络
        WLAN_NOTIFICATION_SOURCE_ALL = 0x0000FFFF  // 所有通知源
    }
    public enum WLAN_NOTIFICATION_ACM : uint
    {
        wlan_notification_acm_start = 0,
        wlan_notification_acm_autoconf_enabled = 1,
        wlan_notification_acm_autoconf_disabled = 2,
        wlan_notification_acm_background_scan_enabled = 3,
        wlan_notification_acm_background_scan_disabled = 4,
        wlan_notification_acm_bss_type_change = 5,
        wlan_notification_acm_power_setting_change = 6,
        wlan_notification_acm_scan_complete = 7,
        wlan_notification_acm_scan_fail = 8,
        wlan_notification_acm_connection_start = 9,
        wlan_notification_acm_connection_complete = 10,
        wlan_notification_acm_connection_attempt_fail = 11,
        wlan_notification_acm_filter_list_change = 12,
        wlan_notification_acm_interface_arrival = 13,
        wlan_notification_acm_interface_removal = 14,
        wlan_notification_acm_profile_change = 15,
        wlan_notification_acm_profile_name_change = 16,
        wlan_notification_acm_profiles_exhausted = 17,
        wlan_notification_acm_network_not_available = 18,
        wlan_notification_acm_network_available = 19,
        wlan_notification_acm_disconnecting = 20,
        wlan_notification_acm_disconnected = 21,
        wlan_notification_acm_adhoc_network_state_change = 22,
        wlan_notification_acm_profile_unblocked = 23,
        wlan_notification_acm_screen_power_change = 24,
        wlan_notification_acm_profile_blocked = 25,
        wlan_notification_acm_scan_list_refresh = 26,
        wlan_notification_acm_operational_state_change = 27,
        wlan_notification_acm_end = 28
    }


    // -------------------- 结构体 --------------------
    [StructLayout(LayoutKind.Sequential)]
    struct WLAN_NOTIFICATION_DATA
    {
        public WLAN_NOTIFICATION_SOURCE NotificationSource;
        public uint NotificationCode;
        public Guid InterfaceGuid;
        public uint dwDataSize;
        public IntPtr pData;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct DOT11_SSID
    {
        public uint uSSIDLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        public byte[] ucSSID;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct WLAN_CONNECTION_NOTIFICATION_DATA
    {
        public WLAN_CONNECTION_MODE wlanConnectionMode;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] // WLAN_MAX_NAME_LENGTH = 256
        public string strProfileName;

        public DOT11_SSID dot11Ssid;
        public DOT11_BSS_TYPE dot11BssType;
        [MarshalAs(UnmanagedType.Bool)]
        public bool bSecurityEnabled;

        public uint wlanReasonCode;
        public uint dwFlags;

        // 变长的 ProfileXml，C# 无法直接固定长度，这里只占位
        public IntPtr strProfileXml;
    }
    enum DOT11_BSS_TYPE : uint
    {
        dot11_BSS_type_infrastructure = 1,
        dot11_BSS_type_independent = 2,
        dot11_BSS_type_any = 3
    }
    enum WLAN_CONNECTION_MODE : uint
    {
        wlan_connection_mode_profile = 0,
        wlan_connection_mode_temporary_profile = 1,
        wlan_connection_mode_discovery_secure = 2,
        wlan_connection_mode_discovery_unsecure = 3,
        wlan_connection_mode_auto = 4,
        wlan_connection_mode_invalid = 5
    }
    // -------------------- 委托 --------------------
    delegate void WLAN_NOTIFICATION_CALLBACK(IntPtr pNotificationData, IntPtr pContext);

    // -------------------- 回调函数 --------------------
    private void NotificationCallback(IntPtr pNotificationData, IntPtr pContext)
    {
        var data = Marshal.PtrToStructure<WLAN_NOTIFICATION_DATA>(pNotificationData);
        switch (data.NotificationSource)
        {
            case WLAN_NOTIFICATION_SOURCE.WLAN_NOTIFICATION_SOURCE_ACM:
                switch ((WLAN_NOTIFICATION_ACM)data.NotificationCode)
                {
                    case WLAN_NOTIFICATION_ACM.wlan_notification_acm_connection_complete:
                        if (data.dwDataSize > 0 && data.pData != null)
                        {
                            var connData = Marshal.PtrToStructure<WLAN_CONNECTION_NOTIFICATION_DATA>(data.pData);
                            //Console.WriteLine("Wi-Fi connected");
                            WifiEventOccurred?.Invoke(this, new WifiEventArgs { ProfileName = connData.strProfileName, Action = "Connected" });
                        }
                        break;
                    case WLAN_NOTIFICATION_ACM.wlan_notification_acm_disconnected:
                        if (data.dwDataSize > 0 && data.pData != null)
                        {
                            var connData = Marshal.PtrToStructure<WLAN_CONNECTION_NOTIFICATION_DATA>(data.pData);
                            //Console.WriteLine("Wi-Fi disconnected");
                            WifiEventOccurred?.Invoke(this, new WifiEventArgs { ProfileName = connData.strProfileName, Action = "Disconnected" });
                        }
                        break;
                    default:
                        break;
                }
                break;
        }
    }

    private IntPtr hClientHandle;  // 放在类里，供各方法使用

    // 委托保持引用，防止被 GC
    private WLAN_NOTIFICATION_CALLBACK callback;
    public class WifiEventArgs : EventArgs
    {
        public string ProfileName { get; set; }
        public string Action { get; set; }
    }

    public event EventHandler<WifiEventArgs> WifiEventOccurred;
    public void Start()
    {
        uint negotiatedVersion;
        uint result = WlanOpenHandle(WLAN_CLIENT_VERSION_LONGHORN, IntPtr.Zero, out negotiatedVersion, out hClientHandle);
        if (result != 0)
        {
            Console.WriteLine("WlanOpenHandle failed: " + result);
            return;
        }

        callback = NotificationCallback; // 保持引用
        uint prevSource;
        result = WlanRegisterNotification(
            hClientHandle,
            WLAN_NOTIFICATION_SOURCE_ACM,
            false,
            Marshal.GetFunctionPointerForDelegate(callback),
            IntPtr.Zero,
            IntPtr.Zero,
            out prevSource
        );

        if (result != 0)
        {
            Console.WriteLine("WlanRegisterNotification failed: " + result);
            return;
        }
        Console.WriteLine("Listening for Wi-Fi events...");
    }

    public void Stop()
    {
        uint prevSource;
        WlanRegisterNotification(hClientHandle, 0, false, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, out prevSource);
        WlanCloseHandle(hClientHandle, IntPtr.Zero);
        Console.WriteLine("Stopped listening.");
    }

}
