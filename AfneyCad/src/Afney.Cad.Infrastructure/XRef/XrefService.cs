using Afney.Cad.Database.Core;
using Afney.Cad.Domain.Abstractions;
using Afney.Cad.Geometry.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Afney.Cad.Infrastructure.XRef;

// Xref (External Reference) Yönetim Servisi
public class XrefService
{
    private readonly CadDatabase _database;
    private readonly List<XrefAttachment> _attachments = new();

    public IReadOnlyList<XrefAttachment> Attachments => _attachments;

    public XrefService(CadDatabase database) => _database = database;

    public XrefAttachment Attach(string filePath, Vector3D insertPoint, double scale = 1.0, double rotation = 0.0)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Xref dosyası bulunamadı: {filePath}");

        var attachment = new XrefAttachment
        {
            Id = Guid.NewGuid(),
            FilePath = filePath,
            FileName = Path.GetFileName(filePath),
            InsertPoint = insertPoint,
            Scale = scale,
            Rotation = rotation,
            Status = XrefStatus.Loaded,
            LastModified = File.GetLastWriteTime(filePath)
        };

        // DWG/DXF dosyasını oku ve entity'leri yükle
        try
        {
            var importer = new Import.CadImporter();
            var entities = importer.Import(filePath);

            var transform = Matrix4x4.TranslationMatrix(insertPoint.X, insertPoint.Y, insertPoint.Z);
            if (Math.Abs(scale - 1.0) > 0.001)
                transform = Matrix4x4.Scaling(scale, scale, scale) * transform;

            foreach (var ent in entities)
            {
                ent.Transform(transform);
                ent.Layer = $"XREF|{attachment.FileName}|{ent.Layer ?? "0"}";
                ent.IsXref = true;
                ent.XrefId = attachment.Id;
                _database.AddEntity(ent);
            }

            attachment.EntityCount = entities.Count;
            attachment.Status = XrefStatus.Loaded;
        }
        catch (Exception ex)
        {
            attachment.Status = XrefStatus.Error;
            attachment.ErrorMessage = ex.Message;
        }

        _attachments.Add(attachment);
        return attachment;
    }

    public void Detach(Guid xrefId)
    {
        var attachment = _attachments.FirstOrDefault(x => x.Id == xrefId);
        if (attachment == null) return;

        // Xref entity'lerini sil
        var xrefEntities = _database.GetAllEntities()
            .Where(e => e.IsXref && e.XrefId == xrefId)
            .ToList();

        foreach (var ent in xrefEntities)
            _database.RemoveEntity(ent.Id);

        _attachments.Remove(attachment);
    }

    public void Reload(Guid xrefId)
    {
        var attachment = _attachments.FirstOrDefault(x => x.Id == xrefId);
        if (attachment == null) return;

        Detach(xrefId);
        Attach(attachment.FilePath, attachment.InsertPoint, attachment.Scale, attachment.Rotation);
    }

    public void ReloadAll()
    {
        foreach (var att in _attachments.ToList())
            Reload(att.Id);
    }

    // Xref dosyası değişmiş mi kontrol et
    public List<XrefAttachment> CheckForUpdates()
    {
        var updated = new List<XrefAttachment>();
        foreach (var att in _attachments)
        {
            if (File.Exists(att.FilePath))
            {
                var lastWrite = File.GetLastWriteTime(att.FilePath);
                if (lastWrite > att.LastModified)
                {
                    att.NeedsReload = true;
                    updated.Add(att);
                }
            }
            else
            {
                att.Status = XrefStatus.NotFound;
            }
        }
        return updated;
    }

    // Bind — Xref'i ana çizime kalıcı olarak dahil et
    public int Bind(Guid xrefId)
    {
        var xrefEntities = _database.GetAllEntities()
            .Where(e => e.IsXref && e.XrefId == xrefId)
            .ToList();

        foreach (var ent in xrefEntities)
        {
            ent.IsXref = false;
            ent.XrefId = null;
            // Layer isminden XREF| prefix'ini kaldır
            if (ent.Layer?.StartsWith("XREF|") == true)
            {
                var parts = ent.Layer.Split('|');
                ent.Layer = parts.Length >= 3 ? parts[2] : "0";
            }
        }

        _attachments.RemoveAll(a => a.Id == xrefId);
        return xrefEntities.Count;
    }
}

public class XrefAttachment
{
    public Guid Id { get; set; }
    public string FilePath { get; set; } = "";
    public string FileName { get; set; } = "";
    public Vector3D InsertPoint { get; set; }
    public double Scale { get; set; } = 1.0;
    public double Rotation { get; set; }
    public XrefStatus Status { get; set; }
    public int EntityCount { get; set; }
    public DateTime LastModified { get; set; }
    public bool NeedsReload { get; set; }
    public string? ErrorMessage { get; set; }
}

public enum XrefStatus { Loaded, NotFound, Error, Unloaded }
