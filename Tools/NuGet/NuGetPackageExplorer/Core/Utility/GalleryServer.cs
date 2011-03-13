﻿using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace NuGet {   
    public class GalleryServer {
        private const string DefaultGalleryServerUrl = "http://go.microsoft.com/fwlink/?LinkID=207106";
        private const string CreatePackageService = "PackageFiles";
        private const string PackageService = "Packages";
        private const string PublishPackageService = "PublishedPackages/Publish";

        private const string _UserAgentPattern = "NuGet Package Explorer/{0} ({1})";
        
        private string _baseGalleryServerUrl;
        private string _userAgent;

        public GalleryServer()
            : this(DefaultGalleryServerUrl) {
        }

        public GalleryServer(string galleryServerSource) {
            _baseGalleryServerUrl = GetSafeRedirectedUri(galleryServerSource);
            var version = typeof(GalleryServer).Assembly.GetNameSafe().Version;
            _userAgent = String.Format(CultureInfo.InvariantCulture, _UserAgentPattern, version, Environment.OSVersion);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Microsoft.Reliability", 
            "CA2000:Dispose objects before losing scope",
            Justification="We dispose it in the Completed event handler.")]
        public void CreatePackage(string apiKey, Stream packageStream, IObserver<int> progressObserver, IPackageMetadata metadata = null) {

            var state = new PublishState {
                PublishKey = apiKey,
                PackageMetadata = metadata, 
                ProgressObserver = progressObserver
            };

            var url = new Uri(String.Format(CultureInfo.InvariantCulture, "{0}/{1}/{2}/nupkg", _baseGalleryServerUrl, CreatePackageService, apiKey));

            WebClient client = new WebClient();
            client.Headers[HttpRequestHeader.ContentType] = "application/octet-stream";
            client.Headers[HttpRequestHeader.UserAgent] = _userAgent;
            client.UploadProgressChanged += OnUploadProgressChanged;
            client.UploadDataCompleted += OnCreatePackageCompleted;
            client.UploadDataAsync(url, "POST", packageStream.ReadAllBytes(), state);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage(
            "Microsoft.Reliability",
            "CA2000:Dispose objects before losing scope",
            Justification = "We dispose it in the Completed event handler.")]
        private void PublishPackage(PublishState state) {
            var url = new Uri(String.Format(CultureInfo.InvariantCulture, "{0}/{1}", _baseGalleryServerUrl, PublishPackageService));

            using (Stream requestStream = new MemoryStream()) {
                var data = new PublishData {
                    Key = state.PublishKey,
                    Id = state.PackageMetadata.Id,
                    Version = state.PackageMetadata.Version.ToString()
                };

                var jsonSerializer = new DataContractJsonSerializer(typeof(PublishData));
                jsonSerializer.WriteObject(requestStream, data);
                requestStream.Seek(0, SeekOrigin.Begin);

                WebClient client = new WebClient();
                client.Headers[HttpRequestHeader.ContentType] = "application/json";
                client.Headers[HttpRequestHeader.UserAgent] = _userAgent;
                client.UploadProgressChanged += OnUploadProgressChanged;
                client.UploadDataCompleted += OnPublishPackageCompleted;
                client.UploadDataAsync(url, "POST", requestStream.ReadAllBytes(), state);
            }
        }

        private void OnCreatePackageCompleted(object sender, UploadDataCompletedEventArgs e) {
            var state = (PublishState) e.UserState;
            if (e.Error != null) {
                Exception error = e.Error;

                WebException webException = e.Error as WebException;
                if (webException != null) {
                    var response = (HttpWebResponse) webException.Response;
                    if (response.StatusCode == HttpStatusCode.InternalServerError) {
                        // real error message is contained inside the response body
                        using (Stream stream = response.GetResponseStream()) {
                            string errorMessage = stream.ReadToEnd();
                            error = new WebException(errorMessage, webException, webException.Status,
                                                     webException.Response);
                        }
                    }
                }
                
                state.ProgressObserver.OnError(error);
            }
            else if (!e.Cancelled) {
                if (state.PackageMetadata != null) {
                    PublishPackage(state);
                }
                else {
                    state.ProgressObserver.OnCompleted();
                }
            }

            var client = (WebClient)sender;
            client.Dispose();
        }

        private void OnPublishPackageCompleted(object sender, UploadDataCompletedEventArgs e) {
            var state = (PublishState)e.UserState;
            if (e.Error != null) {
                Exception error = e.Error;

                WebException webException = e.Error as WebException;
                if (webException != null) {
                    // real error message is contained inside the response body
                    using (Stream stream = webException.Response.GetResponseStream()) {
                        string errorMessage = stream.ReadToEnd();
                        error = new WebException(errorMessage, webException, webException.Status, webException.Response);
                    }
                }

                state.ProgressObserver.OnError(error);
            }
            else if (!e.Cancelled) {
                state.ProgressObserver.OnCompleted();
            }

            var client = (WebClient)sender;
            client.Dispose();
        }

        private void OnUploadProgressChanged(object sender, UploadProgressChangedEventArgs e) {
            var state = (PublishState)e.UserState;
            // Hack: the UploadDataAsync only reports up to 50 percent. multiply by 2 to simulate 100. LOL
            state.ProgressObserver.OnNext(Math.Min(100, 2*e.ProgressPercentage));
        }

        private static string GetSafeRedirectedUri(string uri) {
            WebRequest request = WebRequest.Create(uri);
            try {
                WebResponse response = request.GetResponse();
                if (response == null) {
                    return null;
                }
                return response.ResponseUri.ToString();
            }
            catch (WebException e) {
                return e.Response.ResponseUri.ToString(); ;
            }
        }

        private class PublishState {
            public string PublishKey { get; set; }
            public IObserver<int> ProgressObserver { get; set; }
            public IPackageMetadata PackageMetadata { get; set; }
        }
    }

    [DataContract]
    public class PublishData {
        [DataMember(Name = "key")]
        public string Key { get; set; }

        [DataMember(Name = "id")]
        public string Id { get; set; }

        [DataMember(Name = "version")]
        public string Version { get; set; }
    }
}