using MlsmoonSkillManager.Core.Models;

namespace MlsmoonSkillManager.App.ViewModels;

public static class GitUpdateGuard
{
    public static bool TryBlock(SkillRowViewModel row, out string message, out bool showDialog)
    {
        if (row.Git.Forbidden)
        {
            message = $"{row.Name}: {row.Git.Warning}";
            showDialog = false;
            return true;
        }

        switch (row.Git.State)
        {
            case SkillGitState.Current:
                message = $"{row.Name}: 已与远端对齐，没有可套用的提交。";
                showDialog = false;
                return true;
            case SkillGitState.Ahead:
                message = $"{row.Name} 本机超前远端。↑ Push 不会把提交推到远端，只是说明工作区超前。";
                showDialog = true;
                return true;
            case SkillGitState.Diverged:
                message = $"{row.Name} 本机与远端已分叉，不能快进。请在安装目录里手动处理。";
                showDialog = true;
                return true;
            case SkillGitState.Unclear:
                message = string.IsNullOrWhiteSpace(row.Git.Message)
                    ? $"{row.Name} 本机与远端提交不同，但无法判断谁新，不会自动覆盖。"
                    : $"{row.Name}: {row.Git.Message}";
                showDialog = true;
                return true;
            case SkillGitState.Conflict:
                message = $"{row.Name} 有冲突，无法自动更新。工作区改过这些文件，同时远端也有本机没有的提交。请在安装目录里手动处理后再更新。";
                showDialog = true;
                return true;
            case SkillGitState.LocalChanges:
                message = $"{row.Name} 远端没有新提交，但工作区有本地修改。应用不会覆盖这些改动，请手动处理。";
                showDialog = true;
                return true;
            default:
                message = "";
                showDialog = false;
                return false;
        }
    }
}
