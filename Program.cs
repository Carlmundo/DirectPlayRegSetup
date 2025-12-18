namespace DirectPlay_Reg
{
    using System;
    using Microsoft.Win32;
    using System.Runtime.InteropServices;

    class Program
    {
        const string SERVICE_PROVIDERS_PATH = @"Software\Microsoft\DirectPlay\Service Providers";
        const string SERVICE_PROVIDER_DEFAULT_NAME = "IPX Connection For DirectPlay";
        const string SERVICE_PROVIDER_GUID_STRING = "{685BC400-9D2C-11cf-A9CD-00AA006886E3}";
        const string SERVICE_PROVIDER_PATH = "dpwsockx.dll";

        const string SERVICE_KEY_PATH = @"Software\Microsoft\DirectPlay\Services\{5146ab8cb6b1ce11920c00aa006c4972}";
        const string SERVICE_NAME = "WinSock IPX Connection For DirectPlay";
        const string SERVICE_PATH = "dpwsockx.dll";

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

        const uint MB_ICONINFORMATION = 0x40;
        const uint MB_ICONEXCLAMATION = 0x30;
        const uint MB_YESNO = 0x04;
        const int IDYES = 6;

        static void Main(string[] args)
        {
            bool quiet = false;
            if (args.Length == 1 && args[0] == "/q") {
                quiet = true;
            }
            else if (args.Length != 0) {
                MessageBox(IntPtr.Zero,
                    $"Usage: {AppDomain.CurrentDomain.FriendlyName} [/q]\n\n/q - Silently update registry",
                    "Usage", MB_ICONINFORMATION);
                Environment.Exit(1);
            }

            RegistryKey hkeyServiceProviders;
            try {
                hkeyServiceProviders = Registry.LocalMachine.OpenSubKey(SERVICE_PROVIDERS_PATH, writable: true);
                if (hkeyServiceProviders == null)
                    throw new Exception("not found");
            }
            catch {
                MessageBox(IntPtr.Zero,
                    "Service Providers key not found in registry.\nPlease check DirectX is installed/DirectPlay is enabled.",
                    null, MB_ICONEXCLAMATION);
                return;
            }
            //Create function from here
            RegistryKey hkeyIpxServiceProvider = OpenServiceProviderKey(hkeyServiceProviders, SERVICE_PROVIDER_GUID_STRING);

            bool needToAddSp = false;
            bool needToFixPath = false;

            if (hkeyIpxServiceProvider != null) {
                var pathValue = (string)hkeyIpxServiceProvider.GetValue("Path");
                if (string.IsNullOrEmpty(pathValue) ||
                    !string.Equals(pathValue, SERVICE_PROVIDER_PATH, StringComparison.OrdinalIgnoreCase)) {
                    needToFixPath = true;
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
                    var svcPathValue = (string)hkeyService.GetValue("Path");
                    if (string.IsNullOrEmpty(svcPathValue) ||
                        !string.Equals(svcPathValue, SERVICE_PATH, StringComparison.OrdinalIgnoreCase)) {
                        needToFixServicePath = true;
                    }
                }
            }
            catch {
                MessageBox(IntPtr.Zero, "Error accessing registry.", null, MB_ICONEXCLAMATION);
                return;
            }

            if (!quiet) {
                if (needToAddSp || needToFixPath || needToAddService || needToFixServicePath) {
                    int res = MessageBox(IntPtr.Zero,
                        "Do you want to configure DirectPlay to enable the use of IPX?",
                        "IPXWrapper", MB_YESNO | MB_ICONEXCLAMATION);

                    if (res != IDYES)
                        goto DONE;
                }
                else {
                    MessageBox(IntPtr.Zero,
                        "DirectPlay is already configured for IPX, nothing changed.",
                        "IPXWrapper", MB_ICONINFORMATION);
                    goto DONE;
                }
            }

            // Create or update service provider
            if (needToAddSp) {
                hkeyIpxServiceProvider = hkeyServiceProviders.CreateSubKey(SERVICE_PROVIDER_DEFAULT_NAME);
                hkeyIpxServiceProvider.SetValue("DescriptionA", SERVICE_PROVIDER_DEFAULT_NAME);
                hkeyIpxServiceProvider.SetValue("DescriptionW", SERVICE_PROVIDER_DEFAULT_NAME);
                hkeyIpxServiceProvider.SetValue("dwReserved1", 50, RegistryValueKind.DWord);
                hkeyIpxServiceProvider.SetValue("dwReserved2", 0, RegistryValueKind.DWord);
                hkeyIpxServiceProvider.SetValue("Guid", SERVICE_PROVIDER_GUID_STRING);
            }

            if (needToAddSp || needToFixPath) {
                hkeyIpxServiceProvider.SetValue("Path", SERVICE_PROVIDER_PATH);
            }

            // Create or update service
            if (needToAddService) {
                hkeyService = Registry.LocalMachine.CreateSubKey(SERVICE_KEY_PATH);
                hkeyService.SetValue("Description", SERVICE_NAME);
            }

            if (needToAddService || needToFixServicePath) {
                hkeyService.SetValue("Path", SERVICE_PATH);
            }

            if (!quiet) {
                MessageBox(IntPtr.Zero,
                    "The registry was updated successfully.",
                    "IPXWrapper", MB_ICONINFORMATION);
            }

        DONE:
            hkeyService?.Close();
            hkeyIpxServiceProvider?.Close();
            hkeyServiceProviders?.Close();
        }

        static RegistryKey OpenServiceProviderKey(RegistryKey parentKey, string guid)
        {
            foreach (var subKeyName in parentKey.GetSubKeyNames()) {
                try {
                    var subKey = parentKey.OpenSubKey(subKeyName, writable: true);
                    var existingGuid = (string)subKey.GetValue("Guid");
                    if (string.Equals(existingGuid, guid, StringComparison.OrdinalIgnoreCase)) {
                        return subKey;
                    }
                    subKey.Close();
                }
                catch { }
            }
            return null;
        }
    }

}
