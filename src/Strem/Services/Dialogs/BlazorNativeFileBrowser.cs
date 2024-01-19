﻿using System.Text.RegularExpressions;
using Photino.Blazor;
using Strem.Core.Services.Browsers.File;

namespace Strem.Services.Dialogs;

public class BlazorNativeFileBrowser : IFileBrowser
{
    public static Regex FilterPattern { get; } = new Regex(@"(.*?\|[\;\.\-\*\w]*)\|?");

    public PhotinoBlazorApp App { get; }

    public BlazorNativeFileBrowser(PhotinoBlazorApp app)
    {
        App = app;
    }

    // EXAMPLE "Excel Files (*.xls, *.xlsx)|*.xls;*.xlsx|CSV Files (*.csv)|*.csv"
    public static (string Name, string[] Extensions)[] FilterToBlazorFilter(string filter)
    {
        if (string.IsNullOrEmpty(filter))
        { return Array.Empty<(string Name, string[] Extensions)>(); }

        if (!filter.Contains('|'))
        { return [(filter, new[] { filter })]; }
        
        var filterList = new List<(string Name, string[] Extensions)>();
        var lineFilters = FilterPattern.Matches(filter).Select(x => x.Value);
        foreach (var lineFilter in lineFilters)
        {
            if (!lineFilter.Contains('|'))
            { filterList.Add((lineFilter, new[] { lineFilter })); }
            else
            {
                var filerSections = lineFilter.Split("|");
                filterList.Add((filerSections[0], filerSections[1].Split(";")));
            }
        }

        return filterList.ToArray();
    }

    public string BrowseToOpenFile(string startingDirectory = null, string filterList = null)
    {
        var filters = FilterToBlazorFilter(filterList);
        var result = App.MainWindow.ShowOpenFile(defaultPath: startingDirectory, filters: filters, multiSelect: false);
        return result.Length == 0 ? string.Empty : result[0];
    }
    
    public string BrowseToSaveFile(string startingDirectory = null, string filterList = null)
    {
        var filters = FilterToBlazorFilter(filterList);
        var result = App.MainWindow.ShowSaveFile(defaultPath: startingDirectory, filters: filters);
        return string.IsNullOrEmpty(result) ? string.Empty : result;
    }
}