﻿using System;
using Messages;

namespace Uml.Robotics.Ros
{
    public class AdvertiseServiceOptions<MReq, MRes> where MReq : RosMessage, new() where MRes : RosMessage, new()
    {
        public CallbackQueueInterface callback_queue;
        public string datatype;
        public ServiceCallbackHelper<MReq, MRes> helper;
        public string md5sum;
        public int queue_size;
        public string req_datatype;
        public string res_datatype;
        public string service = "";
        public ServiceFunction<MReq, MRes> srv_func;
        public string srvtype;
        public object tracked_object;

        public AdvertiseServiceOptions(string service, ServiceFunction<MReq, MRes> srv_func)
        {
            // TODO: Complete member initialization
            init(service, srv_func);
        }

        public void init(string service, ServiceFunction<MReq, MRes> callback)
        {
            this.service = service;
            srv_func = callback;
            helper = new ServiceCallbackHelper<MReq, MRes>(callback);
            req_datatype = new MReq().MessageType.Replace("/Request", "__Request");
            res_datatype = new MRes().MessageType.Replace("/Response", "__Response");
            srvtype = req_datatype.Replace("__Request", "");
            datatype = srvtype;
            md5sum = RosService.Generate(srvtype).MD5Sum();
        }
    }
}
