using System.IO.Ports;

namespace Audio_Control_Center_Application.Services
{
    public static class SerialPortService
    {
        public static string[] GetAvailablePorts()
        {
            try
            {
                return SerialPort.GetPortNames().OrderBy(p => p).ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        public static int[] GetCommonBaudRates()
        {
            return new[] { 9600, 19200, 38400, 57600, 115200, 31250, 256000 };
        }
    }
}
