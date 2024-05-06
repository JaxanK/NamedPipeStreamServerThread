# Named Pipe Server Thread
This is a relatively simple tool to start a server thread for a named pipe stream on the windows platform.

I use this to communicate between Java and the dotnet framework. 

I use ClojureCLR to start the Server Thread and evaluate clojure code sent as
strings using the below code. There are many other ways that this code can be
used though.

``` Clojure
  (ns src.Controller.Servers
    #_{:clj-kondo/ignore [:unused-referred-var]}
    (:import (NamedPipeStreamServerThread NamedPipeThread NamedPipeServerMessageProcessor)))


  (defn SetupServer []
    (let [msgInterface (proxy [NamedPipeServerMessageProcessor] []
                        (ProcessMessageAndGetResponse [msg] (pr-str (load-string (str "(in-ns 'src.Controller.core)" msg))))
                        (ProcessError [msg e] (println (str "Error with Message: " msg "... Error is: " (.Message e)))))]

      (def Server_Incoming (new NamedPipeThread msgInterface "ServerIncomingName" 2 true))))

  (defn StartIncomingServer []
    (SetupServer)
    (.StartServer Server_Incoming))

  (defn ShutdownServer []
    (.StopServer ThrawnServer_Incoming))
```