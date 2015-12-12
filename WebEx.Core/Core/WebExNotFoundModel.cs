﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

/// <summary>
/// Summary description for WebExModuleNotFoundModel
/// </summary>
public class WebExModuleNotFoundModel
{
    private string _module;
    private string _view;
    private object _model;
	public WebExModuleNotFoundModel(string module, string view = "", object model = null)
	{
        _module = module;
        _view = view;
        _model = model;
	}

    public string Module
    {
        get
        {
            return _module;
        }
    }
    public string View
    {
        get
        {
            return _view;
        }
    }
    public object Model
    {
        get
        {
            return _model;
        }
    }
}