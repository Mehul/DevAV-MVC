//Copy of WebForms version but removed all unnecessary items because of one view

window.DevAV = (function () {
    var updateTimerID = -1;
    var updateTimeout = 300;
    var searchBoxTimer = -1;
    var cardClassName = "dvCard";
    var cardViewFocusClassName = "focusCard";

    var syncHash = { };

    function setSyncValue(key, value) {
        syncHash[key] = value;
    }
    function getSyncValue(key) {
        return syncHash[key];
    }

    window.setSyncValue = setSyncValue;
    $(document).ready(function (e) {
        var ajax_org = jQuery.ajax;
        jQuery.ajax = function(url, settings){
            if(typeof url === "object"){
                settings = url;
                url = undefined;
            }
            settings.data = $.extend(settings.data, syncHash);
            var params = [ ];
            if(url)
                params.push(url);
            params.push(settings);
            return ajax_org.apply(this, params);
        };
    });

    function getViewModeCore(key) {
        return ASPxClientUtils.GetCookie(key);
    };
    function setViewModeCore(key, value) {
        ASPxClientUtils.SetCookie(key, value);
    };

    var callbackHelper = (function() {
        var callbackControlQueue = [],
            currentCallbackControl = null;

        function doCallback(callbackControl, args, sender) {
            if (!currentCallbackControl) {
                currentCallbackControl = callbackControl;
                if(typeof(DetailsCallbackPanel) !== "undefined" && callbackControl == MainCallbackPanel)
                    DetailsCallbackPanel.cpSkipUpdateDetails = true;
                callbackControl.EndCallback.RemoveHandler(onEndCallback);
                callbackControl.EndCallback.AddHandler(onEndCallback);
                callbackControl.PerformCallback(args);
            } else
                placeInQueue(callbackControl, args, getSenderId(sender));
        };
        function getSenderId(senderObject) {
            if (senderObject.constructor === String)
                return senderObject;
            return senderObject.name || senderObject.id;
        };
        function placeInQueue(callbackControl, args, sender) {
            var queue = callbackControlQueue;
            for (var i = 0; i < queue.length; i++) {
                if (queue[i].control == callbackControl && queue[i].sender == sender) {
                    queue[i].args = args;
                    return;
                }
            }
            queue.push({ control: callbackControl, args: args, sender: sender });
        };
        function onEndCallback(sender) {
            sender.EndCallback.RemoveHandler(onEndCallback);
            currentCallbackControl = null;
            var queuedPanel = callbackControlQueue.shift();
            if (queuedPanel)
                doCallback(queuedPanel.control, queuedPanel.args, queuedPanel.sender);
        }
        return {
            DoCallback: doCallback
        };
    })();

    function UpdateSelectedItemIDValue() {
        var selectedItemID = page.GetSelectedItemID && page.GetSelectedItemID();
        if(selectedItemID !== undefined)
            setSyncValue("SelectedItemID", selectedItemID);
    }

    function updateDetailInfo(sender) {
        UpdateSelectedItemIDValue();

        if(DetailsCallbackPanel.cpSkipUpdateDetails) {
            DetailsCallbackPanel.cpSkipUpdateDetails = false;
            return;
        }
        if(updateTimerID > -1)
            window.clearTimeout(updateTimerID);
        updateTimerID = window.setTimeout(function() {
            window.clearTimeout(updateTimerID);
            callbackHelper.DoCallback(DetailsCallbackPanel, "", sender);
        }, updateTimeout);
    };

    function searchBox_KeyDown(s, e) {
        window.clearTimeout(searchBoxTimer);
        searchBoxTimer = window.setTimeout(function() { onSearchTextChanged(s); }, 1200);
        e = e.htmlEvent;
        if(e.keyCode === 13) {
            if(e.preventDefault)
                e.preventDefault();
            else
                e.returnValue = false;
        }
    };
    function searchBox_TextChanged(s, e) {
        onSearchTextChanged(s);
    };
    function onSearchTextChanged(searchBox) {
        window.clearTimeout(searchBoxTimer);
        var searchText = searchBox.GetText();
        if(getSyncValue("SearchText") == searchText)
            return;
        setSyncValue("SearchText", searchText);
        callbackHelper.DoCallback(MainCallbackPanel, "", searchBox);
    };

    function gridCustomizationWindow_CloseUp() {
        ToolbarMenu.GetItemByName("ColumnsCustomization").SetChecked(false);
    };

    function setToolbarCWItemEnabled(enabled) {
        var item = ToolbarMenu.GetItemByName("ColumnsCustomization");
        if(!item)
            return;
        item.SetEnabled(enabled);
        item.SetChecked(false);        
    }

    var employeePage = (function() {
        function toolbarMenu_ItemClick(s, e) {
            var employeeID = getSelectedEmployeeID();
            var name = e.item.name;
            switch(name) {
                case "GridView":
                    if(isGridViewMode())
                        return;
                    setViewMode(name);
                    callbackHelper.DoCallback(MainCallbackPanel, "", s);
                    break;
                case "CardsView":
                    if(!isGridViewMode())
                        return;
                    setViewMode(name);
                    callbackHelper.DoCallback(MainCallbackPanel, "", s);
                    break;
                case "ColumnsCustomization":
                    if(EmployeesGrid.IsCustomizationWindowVisible())
                        EmployeesGrid.HideCustomizationWindow();
                    else
                        EmployeesGrid.ShowCustomizationWindow(e.htmlElement);
                    break;
                case "New":
                case "Delete":
                case "Meeting":
                case "Task":
                    break;
            }
        }

        function toolbarMenu_Init(s, e) {
            var item = s.GetItemByName("GridView");
            if(item)
                item.SetChecked(isGridViewMode());
            item =  s.GetItemByName("CardsView");
            if(item)
                item.SetChecked(!isGridViewMode());
        }

        function employeesGrid_Init(s, e) {
            setToolbarCWItemEnabled(true);
            updateDetailInfo(s);
        }
        function employeesGrid_FocusedRowChanged(s, e) {
            updateDetailInfo(s);
        }
        function employeesGrid_EndCallback(s, e) {
            updateDetailInfo(s);
        }

        function gridEditButton_Click(e) { };
        function employeeEditButton_Click(s, e) { }

        function evaluationGrid_CustomButtonClick(s, e) { }
        function taskGrid_CustomButtonClick(s, e) { }

        
        function getSelectedEmployeeID() {
            if(isGridViewMode()) {
                var rowIndex = EmployeesGrid.GetFocusedRowIndex();
                if(rowIndex >= 0)
                    return EmployeesGrid.GetRowKey(rowIndex);
            }
            else {
                return getSyncValue("SelectedItemID");
            }
            return null;
        };
        function getViewMode() {
            return getViewModeCore("EmployeeViewMode");
        };
        function setViewMode(value) {
            setViewModeCore("EmployeeViewMode", value);
        };
        function isGridViewMode() {
            var viewMode = getViewMode();
            return !viewMode || viewMode === "GridView";
        };
        function getSelectedItemID() {
            return getSelectedEmployeeID();
        }

    return {
        ToolbarMenu_Init: toolbarMenu_Init,
        ToolbarMenu_ItemClick: toolbarMenu_ItemClick,

        EmployeesGrid_Init: employeesGrid_Init,
        EmployeesGrid_FocusedRowChanged: employeesGrid_FocusedRowChanged,
        EmployeesGrid_EndCallback: employeesGrid_EndCallback,

        GridEditButton_Click: gridEditButton_Click,
        EmployeeEditButton_Click: employeeEditButton_Click,

        EvaluationGrid_CustomButtonClick: evaluationGrid_CustomButtonClick,
        TaskGrid_CustomButtonClick: taskGrid_CustomButtonClick,

        GetSelectedItemID: getSelectedItemID,
        IsGridViewMode: isGridViewMode
    }; 
    })();

    function getCurrentPage() {
        var pageName = DevAVPageName;
        switch (pageName) {
            case "Dashboard":
                return dashboardPage;
            case "Employees":
                return employeePage;
            case "Customers":
                return customerPage;
            case "Products":
                return productPage;
            case "Tasks":
                return taskPage;
        }
    };
    var page = getCurrentPage();

    function adjustMainContentPaneSize() {
        var pane = splitter.GetPaneByName("MainContentPane");
        if(page === employeePage)
            adjustControlSize(pane, page.IsGridViewMode() ? EmployeesGrid : EmployeeCardView, DetailsCallbackPanel);
    }
    function adjustControlSize(splitterPane, grid, detailPanel, minHeight) {
        grid.SetHeight(splitterPane.GetClientHeight() - (detailPanel ? detailPanel.GetHeight() : 0));
    }

    function splitter_PaneResized(s, e) {
        if(e.pane.name == 'MainContentPane')
            window.setTimeout(function() { adjustMainContentPaneSize(); }, 0);
    }

    function mainCallbackPanel_EndCallback(s, e) {
        adjustMainContentPaneSize();
    }

    function toolbarMenu_Init(s, e) {
        page.ToolbarMenu_Init(s, e);
    }

    function toolbarMenu_ItemClick(s, e) {
        page.ToolbarMenu_ItemClick(s, e);
    }
    function gridEditButton_Click(event) {
        page.GridEditButton_Click(event);
        ASPxClientUtils.PreventEventAndBubble(event);
    }


    function cardView_Init(s, e) {
        ASPxClientUtils.AttachEventToElement(s.GetMainElement(), "click", function(evt) {
            var cardID = getCardID(ASPxClientUtils.GetEventSource(evt));
            if(cardID)
                selectCard(cardID, s);
        });
        if(s.cpSelectedItemID)
            selectCard(s.cpSelectedItemID, s);
        
        setToolbarCWItemEnabled(false);
    };
    function cardView_EndCallback(s, e) {
        if(s.cpSelectedItemID)
            selectCard(s.cpSelectedItemID, s);
    };

    function selectCard(id, sender) {
        var card = document.getElementById(id);
        if(!card || card.className.indexOf(cardViewFocusClassName) > -1) 
            return;

        var prevSelectedCard = document.getElementById(getSyncValue("SelectedItemID"));
        if(prevSelectedCard)
            prevSelectedCard.className = ASPxClientUtils.Trim(prevSelectedCard.className.replace(cardViewFocusClassName, ""));

        card.className += " " + cardViewFocusClassName;
        setSyncValue("SelectedItemID", id);
        
        var updateDetails = page === employeePage;
        if(updateDetails)
            callbackHelper.DoCallback(DetailsCallbackPanel, "", sender);
    };
    function getCardID(element) {
        while(element && element.tagName !== "BODY") {
            if(element.className && element.className.indexOf(cardClassName) > -1)
                return element.id;
            element = element.parentNode;
        }
        return null;
    };

    return { 
        Page: page,
        SearchBox_KeyDown: searchBox_KeyDown,
        SearchBox_TextChanged: searchBox_TextChanged,
        MainCallbackPanel_EndCallback: mainCallbackPanel_EndCallback,
        Splitter_PaneResized: splitter_PaneResized,
        ToolbarMenu_Init: toolbarMenu_Init,
        ToolbarMenu_ItemClick: toolbarMenu_ItemClick,
        GridEditButton_Click: gridEditButton_Click,
        GridCustomizationWindow_CloseUp: gridCustomizationWindow_CloseUp,
        CardView_Init: cardView_Init,
        CardView_EndCallback: cardView_EndCallback,
    }; 
})();
