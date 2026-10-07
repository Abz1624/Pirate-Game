using System;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace RoguePirates.V4.Online
{
    /// <summary>How a request ended.</summary>
    public enum ApiOutcome
    {
        Ok,
        /// <summary>No API configured (a build without a production URL).</summary>
        NotConfigured,
        /// <summary>The API could not be reached (no network, refused, timed out, CORS).</summary>
        Offline,
        /// <summary>4xx: the API refused the request (the message says why).</summary>
        Rejected,
        /// <summary>5xx or an unreadable reply.</summary>
        ServerError,
    }

    public sealed class ApiResult<T> where T : class
    {
        public ApiOutcome Outcome;
        public long Status;
        public T Data;
        public string ErrorCode;
        public string Message;

        public bool Ok => Outcome == ApiOutcome.Ok;
        /// <summary>Not configured or not reachable: the online features show "Offline".</summary>
        public bool IsOffline => Outcome == ApiOutcome.Offline || Outcome == ApiOutcome.NotConfigured;

        public override string ToString() => Ok ? $"{Status} ok" : $"{Outcome} {Status} {ErrorCode}: {Message}";
    }

    /// <summary>
    /// Playtest (3 Oct, owner): the playtest API over UnityWebRequest (works on WebGL: no threads; continuations run on
    /// the main thread through Unity's Awaitable). JSON through JsonUtility, the profile token as a Bearer header.
    /// </summary>
    public sealed class ApiClient
    {
        public string BaseUrl { get; }
        public int TimeoutSeconds { get; set; }

        public ApiClient(string baseUrl, int timeoutSeconds)
        {
            BaseUrl = OnlineRules.NormalizeBaseUrl(baseUrl);
            TimeoutSeconds = Mathf.Max(1, timeoutSeconds);
        }

        public bool Configured => !string.IsNullOrEmpty(BaseUrl);

        public Awaitable<ApiResult<T>> Get<T>(string path, string token = null, int timeoutSeconds = 0) where T : class =>
            Send<T>(UnityWebRequest.kHttpVerbGET, path, null, token, timeoutSeconds);

        public Awaitable<ApiResult<T>> Post<T>(string path, object body, string token = null) where T : class =>
            Send<T>(UnityWebRequest.kHttpVerbPOST, path, body, token, 0);

        public Awaitable<ApiResult<T>> Patch<T>(string path, object body, string token = null) where T : class =>
            Send<T>("PATCH", path, body, token, 0);

        public async Awaitable<ApiResult<T>> Send<T>(string method, string path, object body, string token, int timeoutSeconds) where T : class
        {
            var result = new ApiResult<T>();
            if (!Configured)
            {
                result.Outcome = ApiOutcome.NotConfigured;
                result.Message = "Offline";
                return result;
            }
            using (var request = new UnityWebRequest(BaseUrl + path, method))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                if (body != null)
                {
                    var json = body is RawJsonBody raw ? raw.Json : JsonUtility.ToJson(body); // V5 M9a: the run log rides as raw JSON
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                    request.SetRequestHeader("Content-Type", "application/json");
                }
                if (!string.IsNullOrEmpty(token)) request.SetRequestHeader("Authorization", "Bearer " + token);
                request.timeout = timeoutSeconds > 0 ? timeoutSeconds : TimeoutSeconds;
                try
                {
                    await request.SendWebRequest();
                }
                catch (Exception e)
                {
                    // Some platforms surface a failed request as an exception; the request's own state is read below.
                    if (request.result == UnityWebRequest.Result.InProgress)
                    {
                        result.Outcome = ApiOutcome.Offline;
                        result.Message = e.Message;
                        return result;
                    }
                }
                result.Status = request.responseCode;
                var text = request.downloadHandler != null ? request.downloadHandler.text : null;
                switch (request.result)
                {
                    case UnityWebRequest.Result.Success:
                        result.Outcome = ApiOutcome.Ok;
                        if (!string.IsNullOrEmpty(text))
                        {
                            try { result.Data = JsonUtility.FromJson<T>(text); }
                            catch (Exception e)
                            {
                                result.Outcome = ApiOutcome.ServerError;
                                result.Message = "Unreadable reply: " + e.Message;
                            }
                        }
                        break;
                    case UnityWebRequest.Result.ProtocolError:
                        result.Outcome = request.responseCode >= 500 ? ApiOutcome.ServerError : ApiOutcome.Rejected;
                        ReadError(text, result);
                        if (string.IsNullOrEmpty(result.Message)) result.Message = $"HTTP {request.responseCode}";
                        break;
                    default: // ConnectionError (also timeouts and CORS on WebGL), DataProcessingError
                        result.Outcome = request.responseCode >= 500 ? ApiOutcome.ServerError : ApiOutcome.Offline;
                        result.Message = request.error;
                        break;
                }
            }
            return result;
        }

        static void ReadError<T>(string text, ApiResult<T> result) where T : class
        {
            if (string.IsNullOrEmpty(text)) return;
            try
            {
                var error = JsonUtility.FromJson<ApiError>(text);
                if (error == null) return;
                result.ErrorCode = error.error;
                result.Message = error.message;
            }
            catch (Exception) { /* not JSON: keep the status */ }
        }
    }
}
