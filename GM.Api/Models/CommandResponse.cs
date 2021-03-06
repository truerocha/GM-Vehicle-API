﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace GM.Api.Models
{
    /// <summary>
    /// Root object returned by a command request, or a call to a status url
    /// </summary>
    public class CommandRequestResponse
    {
        /// <summary>
        /// Inner response
        /// </summary>
        [JsonProperty("commandResponse")]
        public CommandResponse CommandResponse { get; set; }
    }

    /// <summary>
    /// Command Response Object
    /// </summary>
    public class CommandResponse
    {
        /// <summary>
        /// Timestamp the request was received by the server
        /// </summary>
        [JsonProperty("requestTime")]
        public DateTime RequestTime { get; set; }

        /// <summary>
        /// Timestamp the server completed the request
        /// </summary>
        [JsonProperty("completionTime")]
        public DateTime CompletionTime { get; set; }

        /// <summary>
        /// Status URL to be polled for updates (commands are async)
        /// </summary>
        [JsonProperty("url")]
        public string Url { get; set; }

        /// <summary>
        /// Current status of the command request
        /// (e.g. "inProgress", "success")
        /// </summary>
        [JsonProperty("status")]
        public string Status { get; set; } //inProgress, success

        /// <summary>
        /// Probably refers to the type of the response body
        /// </summary>
        [JsonProperty("type")]
        public string Type { get; set; }

        /// <summary>
        /// Response body for commands that include a response (e.g. diagnostics, location)
        /// </summary>
        [JsonProperty("body")]
        public ResponseBody Body { get; set; }

    }



    /// <summary>
    /// Response Body
    /// Note: this only contains a diagnostic response. there are likely others.
    /// </summary>
    public class ResponseBody
    {
        /// <summary>
        /// Populated for diagnostics command
        /// </summary>
        [JsonProperty("diagnosticResponse")]
        public DiagnosticResponse[] DiagnosticResponse { get; set; }

        /// <summary>
        /// populated for location command
        /// </summary>
        [JsonProperty("location")]
        public Location Location { get; set; }

        /// <summary>
        /// placeholder - not yet tested
        /// </summary>
        [JsonProperty("hotspotInfo")]
        public HotspotInfo HotspotInfo { get; set; }

    }


}
