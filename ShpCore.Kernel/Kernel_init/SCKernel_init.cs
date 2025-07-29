using ShpCore.Logging;
using SharpCore.Abstractions;
using ShpCore.Launcher.Core.Factory;

namespace SharpCore.Kernel.Init;

public class SharpCoreKernel : IKernelEntryPoint
{
    public void Run(string payloadPath, string protocol, string adapter, bool devMode = false)
    {

        if (devMode)
        {
        
            KernelLog.Debug("[CLI MODE] DevMode on. init with your own microkernel.");

            if (!File.Exists(payloadPath))
            {
                KernelLog.Panic($"[Kernel Loader SckernelInit line:19] Payload does not exists on: {payloadPath}");
                return;
            }

            if (!Directory.Exists(adapter))
            {
                KernelLog.Panic($"[Kernel Loader SckernelInit line:25] Adapter path does not exists: {adapter}");
                return;
            }

            try
            {
                var protocolInstance = ProtocolFactory.Get(protocol);
                var bridge = protocolInstance.CreateBridge(adapter);
                bridge.Start();

                var json = File.ReadAllText(payloadPath);
                bridge.Send(json);

                KernelLog.Info("[Kernel Loader] Payload send succesfully.");
            }
            catch (Exception ex)
            {
                KernelLog.Panic("[Kernel Loader SckernelInit line:42] Kernel Execution failure.", ex);
            }

        }    

        KernelLog.Info("[Kernel Loader] Kernel exec terminated.");

    }
}
