// Copyright (c) 2026 EIDO AUTOMATION, S.L.U.

// =========================================================================
// WorkerMessage.cs
// Versioned, bounded IPC envelope. Only explicit data contracts cross inherited process pipes.
//
// Developed by EIDO Automation
// =========================================================================
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;
using EidoTiaWorkbench.Core.Export;
using EidoTiaWorkbench.Core.Import;
using EidoTiaWorkbench.Core.Inventory;
using EidoTiaWorkbench.Core.Model;
using EidoTiaWorkbench.Core.Openness;

namespace EidoTiaWorkbench.Core.Integration {
    /// <summary>Versioned, bounded IPC envelope. Only explicit data contracts cross inherited process pipes.</summary>
    [DataContract]
    public sealed class WorkerMessage {
        private const int MaximumBytes = 64 * 1024 * 1024;

        /// <summary>Gets or sets wire protocol version; only version 1 is accepted.</summary>
        [DataMember]
        public int Protocol { get; set; } = 1;

        /// <summary>Gets or sets command or response discriminator defined by protocol version 1.</summary>
        [DataMember]
        public string Command {
            get; set;
        }

        /// <summary>Gets or sets project or workspace path interpreted by the command.</summary>
        [DataMember]
        public string Path {
            get; set;
        }

        /// <summary>Gets or sets stable Windows process ID for explicit attachment.</summary>
        [DataMember]
        public int Instance {
            get; set;
        }

        /// <summary>Gets or sets a value indicating whether the command option is enabled: UI for Start, close-owned for Disconnect, extended catalog for Scan, or backup for Import.</summary>
        [DataMember]
        public bool Flag {
            get; set;
        }

        /// <summary>Gets or sets a value indicating whether PLC XML normalization is requested.</summary>
        [DataMember]
        public bool Normalize {
            get; set;
        }

        /// <summary>Gets or sets a value indicating whether HMI XML must retain its original export representation.</summary>
        [DataMember]
        public bool PreserveHmi {
            get; set;
        }

        /// <summary>Gets or sets a value indicating whether unchanged export payloads may be skipped.</summary>
        [DataMember]
        public bool Incremental {
            get; set;
        }

        /// <summary>Gets or sets PLC artifact profile used for inventory discovery.</summary>
        [DataMember]
        public ExportProfile Profile {
            get; set;
        }

        /// <summary>Gets or sets explicit import conflict decision; Ask never implies replacement.</summary>
        [DataMember]
        public ImportConflictPolicy Policy {
            get; set;
        }

        /// <summary>Gets or sets export inventory or current export progress item.</summary>
        [DataMember]
        public List<ExportItem> Items {
            get; set;
        }

        /// <summary>Gets or sets repository artifacts selected for preview or import.</summary>
        [DataMember]
        public List<RepositoryArtifact> Artifacts {
            get; set;
        }

        /// <summary>Gets or sets discovered TIA processes without attaching to them.</summary>
        [DataMember]
        public List<TiaPortalInstanceInfo> Instances {
            get; set;
        }

        /// <summary>Gets or sets session snapshot returned after command completion.</summary>
        [DataMember]
        public TiaProjectInfo Project {
            get; set;
        }

        /// <summary>Gets or sets per-artifact export outcome.</summary>
        [DataMember]
        public ExportResult Export {
            get; set;
        }

        /// <summary>Gets or sets per-artifact preview or import outcome.</summary>
        [DataMember]
        public ImportResult Import {
            get; set;
        }

        /// <summary>Gets or sets import progress counters.</summary>
        [DataMember]
        public ImportProgressInfo Progress {
            get; set;
        }

        /// <summary>Gets or sets formatted remote diagnostic text rather than a serialized exception.</summary>
        [DataMember]
        public string Error {
            get; set;
        }

        /// <summary>Gets or sets a value indicating whether cooperative cancellation completed.</summary>
        [DataMember]
        public bool Cancelled {
            get; set;
        }

        /// <summary>Gets or sets severity of a worker diagnostic frame.</summary>
        [DataMember]
        public string LogLevel {
            get; set;
        }

        /// <summary>Gets or sets human-readable worker diagnostic text.</summary>
        [DataMember]
        public string LogText {
            get; set;
        }

        /// <summary>Writes and flushes one length-prefixed XML frame without closing the caller's stream.</summary>
        /// <param name="stream">Writable pipe owned by the caller.</param>
        /// <param name="message">Explicit data-contract envelope to serialize.</param>
        /// <exception cref="InvalidDataException">The serialized frame exceeds 64 MiB.</exception>
        public static void Write(Stream stream, WorkerMessage message) {
            using (var buffer = new MemoryStream()) {
                CreateSerializer().WriteObject(buffer, message);
                if (buffer.Length > MaximumBytes) {
                    throw new InvalidDataException("Worker message exceeds 64 MiB.");
                }

                var size = BitConverter.GetBytes((int)buffer.Length);
                stream.Write(size, 0, size.Length);
                buffer.Position = 0;
                buffer.CopyTo(stream);
                stream.Flush();
            }
        }

        /// <summary>Reads one bounded frame, rejecting DTDs, invalid sizes and incompatible protocol versions.</summary>
        /// <param name="stream">Readable pipe owned by the caller.</param>
        /// <returns>The validated protocol envelope.</returns>
        /// <exception cref="EndOfStreamException">The peer closed before the complete frame arrived.</exception>
        /// <exception cref="InvalidDataException">The frame size or protocol is invalid.</exception>
        public static WorkerMessage Read(Stream stream) {
            var length = new byte[4];
            ReadExactly(stream, length);
            var size = BitConverter.ToInt32(length, 0);
            if (size < 1 || size > MaximumBytes) {
                throw new InvalidDataException("Invalid worker frame size.");
            }

            var payload = new byte[size];
            ReadExactly(stream, payload);
            using (var buffer = new MemoryStream(payload))
            using (var reader = XmlReader.Create(buffer, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaximumBytes })) {
                var message = (WorkerMessage)CreateSerializer().ReadObject(reader);
                if (message.Protocol != 1) {
                    throw new InvalidDataException("Incompatible worker protocol.");
                }

                return message;
            }
        }

        private static DataContractSerializer CreateSerializer() => new DataContractSerializer(typeof(WorkerMessage), new[] { typeof(ReadOnlyCollection<string>), typeof(List<string>), typeof(Dictionary<string, string>) });

        private static void ReadExactly(Stream stream, byte[] buffer) {
            var offset = 0;
            while (offset < buffer.Length) {
                var count = stream.Read(buffer, offset, buffer.Length - offset);
                if (count == 0) {
                    throw new EndOfStreamException("Openness worker disconnected; operation outcome may be incomplete. Inspect the report before retrying.");
                }

                offset += count;
            }
        }
    }
}
