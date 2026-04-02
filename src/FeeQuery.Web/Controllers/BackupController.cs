using FeeQuery.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace FeeQuery.Web.Controllers;

/// <summary>
/// 备份文件下载控制器
/// </summary>
[ApiController]
[Route("api/backup")]
public class BackupController : ControllerBase
{
    private readonly BackupService _backupService;
    private readonly ILogger<BackupController> _logger;

    public BackupController(BackupService backupService, ILogger<BackupController> logger)
    {
        _backupService = backupService;
        _logger = logger;
    }

    /// <summary>
    /// 下载备份文件
    /// </summary>
    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> Download(int id)
    {
        var record = await _backupService.GetBackupRecordAsync(id);
        if (record == null)
            return NotFound("备份记录不存在");

        var filePath = await _backupService.GetBackupFilePathAsync(id);
        if (filePath == null)
            return NotFound("备份文件不存在或已被删除");

        var contentType = record.Format.Equals("Sqlite", StringComparison.OrdinalIgnoreCase)
            ? "application/octet-stream"
            : "application/json";

        _logger.LogInformation("下载备份文件：{FileName}", record.FileName);

        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return File(stream, contentType, record.FileName);
    }
}
