using System.Threading;

namespace CS2GameHelper.Utils;

public abstract class ThreadedServiceBase : IDisposable
{
    private Thread? _thread;
    private volatile bool _isRunning;

    protected virtual string ThreadName => nameof(ThreadedServiceBase);

    protected virtual TimeSpan ThreadTimeout { get; set; } = TimeSpan.FromSeconds(3);

    protected virtual TimeSpan ThreadFrameSleep { get; set; } = TimeSpan.FromMilliseconds(1);

    protected ThreadedServiceBase()
    {
        _thread = new Thread(ThreadStart)
        {
            Name = ThreadName,
            IsBackground = true
        };
    }

    public void Start()
    {
        if (_thread == null)
        {
            throw new ObjectDisposedException(GetType().Name);
        }

        if (_isRunning)
        {
            return;
        }

        _isRunning = true;
        _thread.Start();
    }

    public virtual void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        var thread = Interlocked.Exchange(ref _thread, null);
        if (thread == null)
        {
            return;
        }

        _isRunning = false;
        thread.Interrupt();

        if (!thread.Join(ThreadTimeout))
        {
            thread.Join();
        }
    }

    private void ThreadStart()
    {
        try
        {
            while (_isRunning)
            {
                FrameAction();
                Thread.Sleep(ThreadFrameSleep);
            }
        }
        catch (ThreadInterruptedException)
        {
            // expected during shutdown
        }
        catch (NullReferenceException)
        {
            // legacy behaviour retained for existing services
        }
    }

    protected abstract void FrameAction();
}