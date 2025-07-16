using ShpCore.Logging;
using SharpCore.Abstractions;

namespace SharpCore.Kernel.Init;

public class SharpCoreKernel : IKernelEntryPoint
{
    public void Run(string payloadPath, string protocol, string adapter, bool devMode = false)
    {

        if (devMode)
        {
        
            KernelLog.Debug("[CLI MODE] Modo de desarrollo activado. init con Kernel referenciado localmente.");

            if (!File.Exists(payloadPath))
            {
                KernelLog.Panic($"[Kernel Loader] El payload no existe en la ruta: {payloadPath}");
                return;
            }

            if (!Directory.Exists(adapter))
            {
                KernelLog.Panic($"[Kernel Loader] La ruta del adaptador no existe: {adapter}");
                return;
            }

            try
            {
                var protocolInstance = ProtocolFactory.Get(protocol);
                var bridge = protocolInstance.CreateBridge(adapter);
                bridge.Start();

                var json = File.ReadAllText(payloadPath);
                bridge.Send(json);

                KernelLog.Info("[Kernel Loader] Payload enviado exitosamente.");
            }
            catch (Exception ex)
            {
                KernelLog.Panic("[Kernel Loader] Fallo al ejecutar el kernel.", ex);
            }

        }    

        KernelLog.Info("[Kernel Loader] Ejecución del kernel finalizada.");

    }
}
