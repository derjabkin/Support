﻿using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using DevExpress.ExpressApp;
using DevExpress.ExpressApp.Actions;
using DevExpress.ExpressApp.Model;
using DevExpress.Persistent.Base;
using DevExpress.Xpo;
using Xpand.Persistent.Base.General;
using Xpand.Utils.Helpers;
using XpandTestExecutor.Module.BusinessObjects;

namespace XpandTestExecutor.Module.Controllers {
    public interface IModelOptionsTestExecutor {
        [DefaultValue(5)]
        int ExecutionRetries { get; set; }
    }
    public class TestController : ObjectViewController<ListView, EasyTest>,IModelExtender {
        private const string CancelRun = "Cancel Run";
        private const string Run = "Run";
        private readonly SimpleAction _runTestAction;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly SimpleAction _unlinkTestAction;
        private SingleChoiceAction _selectionModeAction;
        private SingleChoiceAction _userModeAction;
        private bool _isDebug;

        public TestController() {
            _runTestAction = new SimpleAction(this, "RunTest", PredefinedCategory.View) { Caption = Run };
            _runTestAction.Execute += RunTestActionOnExecute;

            _unlinkTestAction = new SimpleAction(this, "UnlinkTest", PredefinedCategory.View) {Caption = "Unlink"};
            _unlinkTestAction.Execute+=UnlinkTestActionOnExecute;   
        }

        protected override void OnActivated() {
            base.OnActivated();
            var testControllerHelper = Application.MainWindow.GetController<TestControllerHelper>();
            _selectionModeAction = testControllerHelper.SelectionModeAction;
            _userModeAction = testControllerHelper.UserModeAction;
            _isDebug = testControllerHelper.ExecutionModeAction.SelectedItem.Caption == "Debug";
        }

        private void UnlinkTestActionOnExecute(object sender, SimpleActionExecuteEventArgs e) {
            var easyTests = e.SelectedObjects.Cast<EasyTest>().ToArray();
            if (ReferenceEquals(_selectionModeAction.SelectedItem.Data, TestControllerHelper.FromFile)) {
                var fileNames = File.ReadAllLines("easytests.txt").Where(s => !string.IsNullOrEmpty(s)).ToArray();
                easyTests = EasyTest.GetTests(ObjectSpace, fileNames);
            }
            foreach (var info in easyTests.SelectMany(test => test.GetCurrentSequenceInfos())) {
                info.WindowsUser = WindowsUser.CreateUsers((UnitOfWork)ObjectSpace.Session(), false).First();
                info.Setup(true);
            }
            ObjectSpace.RollbackSilent();

        }

        private void RunTestActionOnExecute(object sender, SimpleActionExecuteEventArgs e) {
            var isSystemMode = IsSystemMode();
            if (_runTestAction.Caption==CancelRun){
                if (_cancellationTokenSource != null) {
                    _cancellationTokenSource.Cancel();
                    var executionInfo = ObjectSpace.FindObject<ExecutionInfo>(info=>info.Sequence==CurrentSequenceOperator.CurrentSequence);
                    var users = executionInfo.EasyTestRunningInfos.Select(info => info.WindowsUser.Name).ToArray();
                    EnviromentEx.LogOffAllUsers(users);
                }
                _runTestAction.Caption = Run;
            }
            else if (ReferenceEquals(_selectionModeAction.SelectedItem.Data, TestControllerHelper.Selected)) {
                _runTestAction.Caption = CancelRun;
                if (!isSystemMode) {
                    _unlinkTestAction.DoExecute();
                }
                _cancellationTokenSource = TestRunner.Execute(e.SelectedObjects.Cast<EasyTest>().ToArray(), isSystemMode,_isDebug,
                    task => _runTestAction.Caption = Run);}
            else {
                var fileName = Path.Combine(AppDomain.CurrentDomain.SetupInformation.ApplicationBase, "EasyTests.txt");
                TestRunner.Execute(fileName, isSystemMode);
            }
        }

        private bool IsSystemMode() {
            return _selectionModeAction.Active && ReferenceEquals(_userModeAction.SelectedItem.Data, TestControllerHelper.System);
        }

        public void ExtendModelInterfaces(ModelInterfaceExtenders extenders) {
            extenders.Add<IModelOptions, IModelOptionsTestExecutor>();
        }
    }
}