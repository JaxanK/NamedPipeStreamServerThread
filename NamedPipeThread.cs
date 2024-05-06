using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Threading;

namespace NamedPipeStreamServerThread
{
    /// <summary>
    /// This is the class to instantiate a pipe server. Call the constructor with appropriate info, then call StartServer() method
    /// </summary>
    public class NamedPipeThread
    {
        private NamedPipeServerMessageProcessor Processor;
        private string NamedPipeStringName;
        private NamedPipeServerStream? PipeServer;
        private Thread? NewThread = null;
        private bool LoopOnCompletion = false;
        public int NumberOfNamedPipeStreamsUsingThisName = 1;

        public readonly ServerStatus Status = new();

        private CancellationTokenSource CancelToken_NamedPipeThread = new CancellationTokenSource();
        private CancellationToken CancelToken;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="processor">An instantiated object that implements this interface type and is used to process all messages from Clients</param>
        /// <param name="namedPipeStringName">The name to use to connect to the named pipe stream. This is the name your clients also need to know to connect into the pipe server</param>
        /// <param name="token">A cancellation token that can trigger the cancellation and shutdown of the pipeserver</param>
        public NamedPipeThread(NamedPipeServerMessageProcessor processor, string namedPipeStringName,  int numberOfNamedPipeStreamsUsingThisName, bool loopOnCompletion)
        {
            Processor = processor;
            NamedPipeStringName = namedPipeStringName;
            Status.State = ServerState.NotStarted;
            PipeServer = null;
            CancelToken = CancelToken_NamedPipeThread.Token;
            LoopOnCompletion = loopOnCompletion;
            NumberOfNamedPipeStreamsUsingThisName = numberOfNamedPipeStreamsUsingThisName;
        }

        public void StopServer()
        {
            //Cancel tokens replace old way of killing thread. Thread should end itself once cancel token completes
            CancelToken_NamedPipeThread.Cancel();
        }

        /// <summary>
        /// Call this method after the constructor to start the pipe server
        /// </summary>
        public void StartServer()
        {
            if (Status.State != ServerState.NotStarted ) return; //Prevent server from being started twice or during errors
            NewThread = new Thread(new ThreadStart(server))
            {
                IsBackground = true
            };
            NewThread.Start();

        }
        private void server() 
        { 
            //Heavily modified from: http://v01ver-howto.blogspot.com/2010/04/howto-use-named-pipes-to-communicate.html
            Status.State = ServerState.Running;
            do 
            {
                if (CancelToken.IsCancellationRequested)
                {
                    Status.State = ServerState.CancelledByToken;
                    return;
                }
                try
                {
                    //Create pipe instance
                    PipeServer = new NamedPipeServerStream(NamedPipeStringName, PipeDirection.InOut, NumberOfNamedPipeStreamsUsingThisName);
                    //System.Diagnostics.Debug.WriteLine(NamedPipeStringName + " thread created.");
                }
                catch (Exception e)
                {
                    if (e.Message.ToUpper().Contains("ALL PIPE INSTANCES ARE BUSY"))
                    {
                        Status.State = ServerState.AllInstancesBusyError;
                        return;
                    }
                    else
                    {
                        Status.State = ServerState.ErrorMessage;
                        Status.ErrorMessage = e.Message;
                        return;
                    }
                }

                //wait for connection
                //System.Diagnostics.Debug.WriteLine(NamedPipeStringName + " Wait for a client to connect");
                //pipeServer.WaitForConnection(); //Old method before cancellation token
                try
                {
                    PipeServer.WaitForConnectionAsync(CancelToken).Wait(CancelToken);
                }
                catch (System.OperationCanceledException) { }
                if (CancelToken.IsCancellationRequested)
                {
                    PipeServer.Dispose();
                    PipeServer.Close();
                    Status.State = ServerState.CancelledByToken;
                    return;
                }

                //System.Diagnostics.Debug.WriteLine(NamedPipeStringName + " Client connected.");
                try
                {
                    // Streams for the request & Response
                    StreamReader sr = new StreamReader(PipeServer);
                    StreamWriter sw = new StreamWriter(PipeServer);
                    sw.AutoFlush = true;

                    // Read request from the stream.
                    string? msg = sr.ReadLine();
                    //System.Diagnostics.Debug.WriteLine(msg);
                    string msgResponse = "";
                    try
                    {
                        //Call whatever processor has been provided and wait for response
                        msgResponse = Processor.ProcessMessageAndGetResponse((msg ?? "Message is Null"));
                    }
                    catch (Exception e)
                    {
                        msgResponse = "\"Error: " + e.Message + "\"";
                    }

                    Thread.Sleep(200);

                    // Write response to the stream.
                    sw.WriteLine(msgResponse);

                    PipeServer.Disconnect();
                }

                catch (Exception e)
                {
                    //Note this was previously an IOException but changed it to a general exception... ideally any other error handling would occur on the side of the processor
                    //Pass error object to the Processor since we do not want to deal with or care about it here
                    Processor.ProcessError(NamedPipeStringName + " ERROR", e);
                }

                PipeServer.Close();
            } while (LoopOnCompletion);
            Status.State = ServerState.NotStarted;
        }
    }
    public enum ServerState {NotStarted, Running, AllInstancesBusyError, CancelledByToken, ErrorMessage }

    public class ServerStatus
    {
        public ServerState State = ServerState.Running;
        public string? ErrorMessage = null;
    }


    /// <summary>
    /// This interface contains that method that the NamedPipeServer class will call to process a message. It needs to be implemented and passed into the contructor for NamedPipeServer class.
    /// </summary>
    public interface NamedPipeServerMessageProcessor
    {
        public abstract string ProcessMessageAndGetResponse(string message);
        public abstract void ProcessError(string infoMessage, Exception e);
        
    }

    //public class Runner
    //{
    //    string msg;
    //    NamedPipeServerMessageProcessor Processor;

    //    public Runner(string msg, NamedPipeServerMessageProcessor Processor)
    //    {
    //        this.msg = msg;
    //        this.Processor = Processor;
    //    }
    //    public void Run()
    //    {
    //        int tries = 0;
    //        int maxTries = 5;
    //        string message = "";
    //        while (tries < maxTries)
    //        {
    //            tries++;
    //            try
    //            {
    //                //Call whatever processor has been provided and wait for response
    //                Processor.ProcessMessageAndGetResponse(msg);
    //                return;
    //            }
    //            catch (Exception e)
    //            {
    //                message = e.Message;
    //                if (tries == 1) System.Windows.Forms.MessageBox.Show(message);
    //            }
    //            Thread.Sleep(1000);
    //        }
    //        System.Windows.Forms.MessageBox.Show("Thread Error Ocurred " + maxTries + " times with Message:\n" + message + "\n\n Thrawn Message: \n" + msg);

    //    }
    //}

}