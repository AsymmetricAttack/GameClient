using System;
using System.Runtime.InteropServices;

namespace Qualcomm.Snapdragon.Spaces
{
    public partial class BaseRuntimeFeature
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate XrResult xrGetSystemPropertiesDelegate(ulong instance, ulong systemId, IntPtr /*XrSystemProperties*/ systemProperties);
        private static xrGetSystemPropertiesDelegate _xrGetSystemProperties;

        private XrSystemProperties _systemProperties;

        internal XrSystemProperties SystemProperties => _systemProperties;

        internal virtual void OnGetSystemProperties()
        {
            Internal_SetMaxCompositionLayerCount(SystemProperties.GetGraphicsProperties().MaxLayerCount);
        }

        private XrResult Internal_GetSystemProperties()
        {
            using ScopePtr<XrSystemProperties> systemPropertiesPtr = new();
            _systemProperties = new XrSystemProperties(SystemIDHandle);
            systemPropertiesPtr.Copy(_systemProperties);

            XrResult result = _xrGetSystemProperties(InstanceHandle, SystemIDHandle, systemPropertiesPtr.Raw);
            if (result == XrResult.XR_SUCCESS)
            {
                _systemProperties = systemPropertiesPtr.AsStruct();
                OnGetSystemProperties();
            }

            return result;
        }
    }
}
