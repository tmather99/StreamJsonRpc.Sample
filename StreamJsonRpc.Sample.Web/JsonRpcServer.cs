using System;
using System.Threading;
using System.Threading.Tasks;

namespace StreamJsonRpc.Sample.Web.Controllers
{
    public class JsonRpcServer
    {
        /// <summary>
        /// Occurs every second. Just for the heck of it.
        /// </summary>
        public event EventHandler<int> Tick;

        public bool isCancel = false;

        public int Add(int a, int b)
        {
            int result = a + b;
            Console.WriteLine($"  Adding {a} + {b} = {result}");
            return result;
        }

        public void CancelTickOperation(Guid guid)
        {
            isCancel = true;
            Console.WriteLine($"Cancel Tick Operation for {guid}");
        }

        public async Task SendTicksAsync(Guid guid, CancellationToken cancellationToken)
        {
            int tickNumber = 0;
            while (!this.isCancel && !cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(1000, cancellationToken);
                this.Tick?.Invoke(this, ++tickNumber);
                Console.WriteLine($"{guid} - #{tickNumber}");
            }
        }
    }
}
