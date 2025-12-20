namespace DirectPlay_Reg
{
    using Microsoft.Win32;
    using System;
    using System.IO;
    using System.Runtime.InteropServices;

    class Program
    {
        /*
         * Originally converted to C# from https://github.com/solemnwarning/ipxwrapper/blob/master/src/dplay-setup.c
         * Adapted to support TCP
        */
        const string ERROR_REG_ACCESS = "Error accessing registry: ";
        const string DIRECTPLAY_PATH = @"Software\Microsoft\DirectPlay";
        const string PATH_STRING = "Path";
        const string SERVICE_PROVIDERS_PATH = DIRECTPLAY_PATH + "\\Service Providers";
        const string SERVICE_PROVIDER_PATH = "dpwsockx.dll";
        const string SERVICES_PATH = DIRECTPLAY_PATH + "\\Services";
        const string SERVICE_PATH = "dpwsockx.dll";

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

        const uint MB_ICONINFORMATION = 0x40;
        const uint MB_ICONEXCLAMATION = 0x30;
        const uint MB_YESNO = 0x04;
        const int IDYES = 6;

        public static class global
        {
            public static string appName = "DirectPlay Registry Setup";
            public static bool quiet = false;
            public static RegistryKey hkeyServiceProviders;
        }

        static void Main(string[] args)
        {
            if (args.Length >= 1 && args[0] == "/q") {
                global.quiet = true;
            }
            else if (args.Length != 0) {
                MessageBox(IntPtr.Zero,
                    $"Usage: {AppDomain.CurrentDomain.FriendlyName} [/q]\n\n/q - Silently update registry",
                    "Usage", MB_ICONINFORMATION);
                Environment.Exit(1);
            }
            try {
                global.hkeyServiceProviders = Registry.LocalMachine.OpenSubKey(SERVICE_PROVIDERS_PATH, writable: true);
                if (global.hkeyServiceProviders == null) {
                    throw new Exception("NOT_FOUND");
                }
            }       
            catch (Exception e) {
                if (e.Message == "NOT_FOUND") {
                    MessageBox(IntPtr.Zero,
                        "Service Providers key not found in registry.\nPlease check DirectX is installed/DirectPlay is enabled.",
                        null, MB_ICONEXCLAMATION);
                }
                else {
                    MessageBox(IntPtr.Zero, ERROR_REG_ACCESS + e.Message, null, MB_ICONEXCLAMATION);
                }
                return;
            }

            //Set-up registry
            RegSetup("TCP", "Internet TCP/IP Connection For DirectPlay", "{36E95EE0-8577-11CF-960C-0080C7534E82}", "{5046ab8cb6b1ce11920c00aa006c4972}", "WinSock TCP Connection For DirectPlay", 500, 0, "dpnhpast.dll");
            RegSetup("IPX", "IPX Connection For DirectPlay", "{685BC400-9D2C-11cf-A9CD-00AA006886E3}", "{5146ab8cb6b1ce11920c00aa006c4972}", "WinSock IPX Connection For DirectPlay", 50, 0);

            global.hkeyServiceProviders?.Close();
        }

        static void RegSetup(string SERVICE_PROVIDER_SHORT_NAME, string SERVICE_PROVIDER_DEFAULT_NAME, string SERVICE_PROVIDER_GUID_STRING, string SERVICE_KEY, string SERVICE_NAME, Int32 dwReserved1, Int32 dwReserved2, string Gateway="")
        {
            string SERVICE_KEY_PATH = SERVICES_PATH + "\\" + SERVICE_KEY;
            RegistryKey hkeyServiceProvider = OpenServiceProviderKey(global.hkeyServiceProviders, SERVICE_PROVIDER_GUID_STRING);

            bool needToAddSp = false;
            bool needToFixPath = false;

            if (hkeyServiceProvider != null) {
                try {
                    string pathValue = (string)hkeyServiceProvider.GetValue(PATH_STRING);
                    if (string.IsNullOrEmpty(pathValue) ||
                    !string.Equals(pathValue, SERVICE_PROVIDER_PATH, StringComparison.OrdinalIgnoreCase)
                    ) {
                        needToFixPath = true;
                    }
                    else {
                        RegistryValueKind pathKind = hkeyServiceProvider.GetValueKind(PATH_STRING);
                        if (pathKind != RegistryValueKind.String) {
                            needToFixPath = true;
                        }
                    }
                }
                catch {
                    needToAddSp = true;
                }
            }
            else {
                needToAddSp = true;
            }

            bool needToAddService = false;
            bool needToFixServicePath = false;
            RegistryKey hkeyService = null;

            try {
                hkeyService = Registry.LocalMachine.OpenSubKey(SERVICE_KEY_PATH, writable: true);
                if (hkeyService == null) {
                    needToAddService = true;
                }
                else {
                    string svcPathValue = (string)hkeyService.GetValue(PATH_STRING);
                    if (string.IsNullOrEmpty(svcPathValue) ||
                        !string.Equals(svcPathValue, SERVICE_PATH, StringComparison.OrdinalIgnoreCase)
                        ) {
                        needToFixServicePath = true;
                    }
                    else{
                        RegistryValueKind svcPathKind = hkeyService.GetValueKind(PATH_STRING);
                        if (svcPathKind != RegistryValueKind.ExpandString) {
                            needToFixServicePath = true;
                        }
                    }
                }
            }
            catch {
                needToAddService = true;
            }

            if (!global.quiet) {
                if (needToAddSp || needToFixPath || needToAddService || needToFixServicePath) {
                    int res = MessageBox(IntPtr.Zero,
                        "Do you want to configure DirectPlay to enable the use of "+SERVICE_PROVIDER_SHORT_NAME+"?",
                        global.appName, MB_YESNO | MB_ICONEXCLAMATION);

                    if (res != IDYES)
                        goto DONE;
                }
                else {
                    MessageBox(IntPtr.Zero,
                        "DirectPlay is already configured for "+SERVICE_PROVIDER_SHORT_NAME+".\nNo changes have been made.",
                        global.appName, MB_ICONINFORMATION);
                    goto DONE;
                }
            }
            
            try {
                // Create or update service provider
                if (needToAddSp) {
                    hkeyServiceProvider = global.hkeyServiceProviders.CreateSubKey(SERVICE_PROVIDER_DEFAULT_NAME);
                    hkeyServiceProvider.SetValue("DescriptionA", SERVICE_PROVIDER_DEFAULT_NAME);
                    hkeyServiceProvider.SetValue("DescriptionW", SERVICE_PROVIDER_DEFAULT_NAME);
                    hkeyServiceProvider.SetValue("dwReserved1", dwReserved1, RegistryValueKind.DWord);
                    hkeyServiceProvider.SetValue("dwReserved2", dwReserved2, RegistryValueKind.DWord);
                    hkeyServiceProvider.SetValue("Guid", SERVICE_PROVIDER_GUID_STRING);
                    if (Gateway != "") {
                        hkeyServiceProvider.SetValue("Gateway", Gateway);
                    }
                }
                if (needToAddSp || needToFixPath) {
                    hkeyServiceProvider.SetValue("Path", SERVICE_PROVIDER_PATH);
                }
                // Create or update service
                if (needToAddService) {
                    hkeyService = Registry.LocalMachine.CreateSubKey(SERVICE_KEY_PATH);
                    hkeyService.SetValue("Description", SERVICE_NAME);
                }

                if (needToAddService || needToFixServicePath) {
                    hkeyService.SetValue("Path", SERVICE_PATH, RegistryValueKind.ExpandString);
                }
            }
            catch (Exception e) {
                MessageBox(IntPtr.Zero, ERROR_REG_ACCESS + e.Message, null, MB_ICONEXCLAMATION);
                return;
            }      

            if (!global.quiet) {
                MessageBox(IntPtr.Zero,
                    "The registry was updated successfully.",
                    global.appName, MB_ICONINFORMATION);
            }

            DONE:
                hkeyService?.Close();
                hkeyServiceProvider?.Close();
        }
        static RegistryKey OpenServiceProviderKey(RegistryKey parentKey, string guid)
        {
            string strGuid = "Guid";
            foreach (var subKeyName in parentKey.GetSubKeyNames()) {
                try {
                    var subKey = parentKey.OpenSubKey(subKeyName, writable: true);
                    var existingGuid = (string)subKey.GetValue(strGuid);
                    if (string.Equals(existingGuid, guid, StringComparison.OrdinalIgnoreCase)) {
                        RegistryValueKind subKeyKind = subKey.GetValueKind(strGuid);
                        if (subKeyKind == RegistryValueKind.String) {
                            return subKey;
                        }       
                    }
                    subKey.Close();
                }
                catch {}
            }
            return null;
        }
    }

}
