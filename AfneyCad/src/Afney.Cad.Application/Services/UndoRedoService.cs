using System;
using System.Collections.Generic;

namespace Afney.Cad.Application.Services;

// Gelişmiş Undo/Redo — etiket, memory limit, grup işlemleri, zaman damgası
public class UndoRedoService
{
    private readonly List<UndoEntry> _undoStack = new();
    private readonly List<UndoEntry> _redoStack = new();
    public int MaxStackSize { get; set; } = 200;
    public long MaxMemoryBytes { get; set; } = 100 * 1024 * 1024; // 100MB
    private long _currentMemoryUsage;

    public int UndoCount => _undoStack.Count;
    public int RedoCount => _redoStack.Count;
    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    // Yeni işlem kaydet
    public void Push(string label, Action undoAction, Action redoAction, long estimatedBytes = 1024)
    {
        var entry = new UndoEntry
        {
            Label = label,
            UndoAction = undoAction,
            RedoAction = redoAction,
            Timestamp = DateTime.Now,
            EstimatedBytes = estimatedBytes
        };

        _undoStack.Add(entry);
        _redoStack.Clear(); // Yeni işlem sonrası redo geçersiz

        _currentMemoryUsage += estimatedBytes;

        // Stack ve memory limiti kontrol
        while (_undoStack.Count > MaxStackSize || _currentMemoryUsage > MaxMemoryBytes)
        {
            if (_undoStack.Count == 0) break;
            _currentMemoryUsage -= _undoStack[0].EstimatedBytes;
            _undoStack.RemoveAt(0);
        }
    }

    // Undo
    public string? Undo()
    {
        if (_undoStack.Count == 0) return null;
        var entry = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);

        entry.UndoAction();
        _redoStack.Add(entry);

        return entry.Label;
    }

    // Redo
    public string? Redo()
    {
        if (_redoStack.Count == 0) return null;
        var entry = _redoStack[^1];
        _redoStack.RemoveAt(_redoStack.Count - 1);

        entry.RedoAction();
        _undoStack.Add(entry);

        return entry.Label;
    }

    // Undo stack etiketlerini getir (UI menü için)
    public List<string> GetUndoLabels(int maxCount = 20)
    {
        var labels = new List<string>();
        for (int i = _undoStack.Count - 1; i >= 0 && labels.Count < maxCount; i--)
            labels.Add($"{_undoStack[i].Label} ({_undoStack[i].Timestamp:HH:mm:ss})");
        return labels;
    }

    public List<string> GetRedoLabels(int maxCount = 20)
    {
        var labels = new List<string>();
        for (int i = _redoStack.Count - 1; i >= 0 && labels.Count < maxCount; i--)
            labels.Add($"{_redoStack[i].Label} ({_redoStack[i].Timestamp:HH:mm:ss})");
        return labels;
    }

    // Belirli bir noktaya kadar undo (çoklu geri alma)
    public int UndoTo(int count)
    {
        int undone = 0;
        for (int i = 0; i < count && CanUndo; i++)
        {
            Undo();
            undone++;
        }
        return undone;
    }

    // Stack temizle
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        _currentMemoryUsage = 0;
    }

    // Memory kullanım raporu
    public UndoMemoryStats GetMemoryStats()
    {
        return new UndoMemoryStats
        {
            UndoCount = _undoStack.Count,
            RedoCount = _redoStack.Count,
            TotalMemoryBytes = _currentMemoryUsage,
            MaxMemoryBytes = MaxMemoryBytes,
            UsagePercent = MaxMemoryBytes > 0 ? (double)_currentMemoryUsage / MaxMemoryBytes * 100 : 0
        };
    }
}

public class UndoEntry
{
    public string Label { get; set; } = "";
    public Action UndoAction { get; set; } = () => { };
    public Action RedoAction { get; set; } = () => { };
    public DateTime Timestamp { get; set; }
    public long EstimatedBytes { get; set; }
}

public class UndoMemoryStats
{
    public int UndoCount { get; set; }
    public int RedoCount { get; set; }
    public long TotalMemoryBytes { get; set; }
    public long MaxMemoryBytes { get; set; }
    public double UsagePercent { get; set; }
}
