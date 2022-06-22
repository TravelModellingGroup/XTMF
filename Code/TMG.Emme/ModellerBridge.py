﻿'''
    Copyright 2014-2022 Travel Modelling Group, Department of Civil Engineering, University of Toronto

    This file is part of XTMF.

    XTMF is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    XTMF is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with XTMF.  If not, see <http://www.gnu.org/licenses/>.
'''

from __future__ import print_function
import sys
import os
from os.path import exists
import glob
import time
import math
import array
import inspect
import timeit
import inro.modeller
import traceback as _traceback
import inro.modeller as _m
from inro.emme.desktop import app as _app
from threading import Thread
import threading
import time
from contextlib import contextmanager
import six

class ProgressTimer(Thread):
    def __init__(self, delegateFunction, XtmfBridge):
        self._stopped = False
        self.delegateFunction = delegateFunction
        self.bridge = XtmfBridge
        Thread.__init__(self)
        self.run = self._run
    
    def _run(self):
        try:
            while not self._stopped:
                progressTuple = self.delegateFunction()
                self.bridge.ReportProgress(float(progressTuple[2] - progressTuple[0]) / float(progressTuple[1] - progressTuple[0]))
                time.sleep(0.01667)
        except:
            # silently fail if we are unable to understand what the progress tuple is doing.
            pass
    
    def stop(self):
        self._stopped = True

# A Stream that does nothing
class NullStream:
    # Do nothing
     def __init__(self): 
         pass 
     # fake the write method
     def write(self, data): 
         pass

     def flush(self):
        pass

# A Stream which redirects print statements to XTMF Console
class RedirectToXTMFConsole:
    def __init__(self, xtmfBridge):
        self.bridge = xtmfBridge
    
    def write(self, data):
        self.bridge.SendPrintSignal(str(data))

    def flush(self):
        pass

def RedirectLogbookWrite(name, attributes=None, value=None):
    pass

@contextmanager
def RedirectLogbookTrace(name, attributes=None, value=None, save_arguments=None):
    try:
        yield None
    finally:
        pass

class XTMFBridge:
    """The stream used for sending data to XTMF"""
    ToXTMF = None
    """The stream used for getting data from XTMF"""
    FromXTMF = None
    """Our link to the EMME modeller"""
    Modeller = None
    """The name of the field that XTMF enabled Modeller Tools will use"""
    _XTMFCallParameters = "XTMFCallParameters"
    
    # Message numbers
    """Tell XTMF that we are ready to start accepting messages"""
    SignalStart = 0
    """Tell XTMF that we exited / XTMF is telling us to exit"""
    SignalTermination = 1
    """XTMF is telling us to start up a tool"""
    SignalStartModule = 2
    """Tell XTMF that we have successfully ran the requested tool"""
    SignalRunComplete = 3
    """Tell XTMF that we have had an error when creating the parameters"""
    SignalParameterError = 4
    """Tell XTMF that we have had an error while running the tool"""
    SignalRuntimeError = 5
    """XTMF says we need to clean out the modeller log book"""
    SignalCleanLogbook = 6
    """We say that we need to generate a progress report for XTMF"""
    SignalProgressReport = 7
    """Tell XTMF that we have successfully ran the requested tool"""
    SignalRunCompleteWithParameter = 8
    """XTMF is requesting a check if a Tool namespace exists"""
    SignalCheckToolExists = 9
    """Tell XTMF that we have had an error finding the requested tool"""
    SignalSendToolDoesNotExistsError = 10
    """Tell XTMF that a print statement has been encountered and to write to the Run Console"""
    SignalSendPrintMessage = 11
    """Signal from XTMF to disable writing to logbook"""
    SignalDisableLogbook = 12
    """Signal from XTMF to enable writing to logbook"""
    SignalEnableLogbook = 13    
    """Signal from XTMF to start up a tool using binary parameters"""
    SignalStartModuleBinaryParameters = 14
    """Signal from XTMF to check all loaded toolboxes to ensure that all unconsolidated tools actually point to a real script file."""
    SignalCheckForMissingTools = 15
        
    """Initialize the bridge so that the tools that we run will not accidentally access the standard I/O"""
    def __init__(self, emmeApplication, databankName):
        self.CachedLogbookWrite = _m.logbook_write
        self.CachedLogbookTrace = _m.logbook_trace
        self.previous_level = None

        # Redirect sys.stdout
        sys.stdin.close()
        terminate = False
        if emmeApplication is not None:
            # Load up Modeller before continuing on
            try:
                self.emmeApplication = emmeApplication
                if databankName is not None:
                    self.SwitchToDatabank(emmeApplication, databankName)
                self.Modeller = inro.modeller.Modeller(emmeApplication)
                _m.logbook_write("Activated modeller from ModellerBridge for XTMF")
            except:
                #Terminate the bridge if we are unable to
                terminate = True
        else:
            terminate = True
        self.ToXTMF = open('\\\\.\\pipe\\' + pipeName, 'wb', 0)
        self.FromXTMF = os.fdopen(0, "rb")
        sys.stdout = NullStream()
        self.IOLock = threading.Lock()
        sys.stdin = None
        sys.stdout = RedirectToXTMFConsole(self)
        if terminate:
            exit(-1)
        return

    def GetToolParameters(self, tool):
        if six.PY3:
            return inspect.getfullargspec(tool.__call__)[0][1:]
        else:
            return inspect.getargspec(tool.__call__)[0][1:]
    
    def ReadLEB(self):
        ret = 0
        Continue = True
        Continues = 0
        bitIndex = 0
        while Continue:
            #unsigned array
            byteArray = array.array('B')
            byteArray.fromfile(self.FromXTMF, 1)
            current = byteArray.pop()
            if current < 128:
                Continue = False
            else:
                current -= 128
            # Add together the numbers
            ret = ret + (current << bitIndex)
            bitIndex += 7
            #ret = (ret << 7) + current
        return ret
        
    def ReadString(self):
        # The byte length will be twice the number of characters
        stringArray = array.array('u')
        length = self.ReadLEB() >> (stringArray.itemsize >> 1)
        try:
            stringArray.fromfile(self.FromXTMF, length)
            ret = stringArray.tounicode()
            return ret
        except Exception as e:
            print(str(stringArray))
            print(str(e))
            return stringArray.tounicode()
    
    def ReadInt(self):
        intArray = array.array('l')
        intArray.fromfile(self.FromXTMF, 1)
        return intArray.pop()
    
    def IsWhitespace(self, c):
        return (c == ' ') or (c == '\t') or (c == '\s')
    
    def CreateTool(self, toolName):
        return self.Modeller.tool(toolName)
    
    def GetToolParameterTypes(self, tool):
        # get the names of the parameters
        parameterNames = self.GetToolParameters(tool)
        ret = []
        for param in parameterNames:
            try:
                paramVar = eval("tool.__class__." + str(param))
            except:
                _m.logbook_write("A parameter with the name '" + param + "' does not exist in the executing EMME tool!  Make sure that the EMME tool defines this attribute as a class variable.")
                self.SendParameterError("A parameter with the name '" + param + "' does not exist in the executing EMME tool!  Make sure that the EMME tool defines this attribute as a class variable.")
                return None
            typeOfParam = paramVar.type
            if typeOfParam == _m.Attribute(float).type:
                ret.append("float")
            elif typeOfParam == _m.Attribute(int).type:
                ret.append("int")
            elif typeOfParam == _m.Attribute(str).type:
                ret.append("string")
            elif typeOfParam == _m.Attribute(bool).type:
                ret.append("bool")
            else:
                _m.logbook_write(param + " uses a type unsupported by the ModellerBridge '" + str(typeOfParam) + "'!")
                self.SendParameterError(param + " uses a type unsupported by the ModellerBridge '" + str(typeOfParam) + "'!")
                return None
        return ret 
    
    def BreakIntoParametersStrings(self, parameterString):
        parameterList = []
        currentlyBuilding = False
        currentParameter = str()
        state = 0
        # execute a FSA to parse the string and extract out the parameters
        for i in range(len(parameterString)):
            c = parameterString[i]
            # initial state, checking to see if the next parameter starts with
            # " or not
            if state == 0:
                if c == '\"':
                    state = 2
                # ignore whitespace until we find the next parameter
                elif not self.IsWhitespace(c):
                    currentlyBuilding = True
                    currentParameter = currentParameter + c
                    state = 1
            # We are currently waiting for a whitespace to end this parameter
            elif state == 1:
                if self.IsWhitespace(c):
                    currentlyBuilding = False
                    parameterList.append(currentParameter)
                    currentParameter = str()
                    state = 0
                else:
                    currentlyBuilding = True
                    currentParameter = currentParameter + c
            # We are currently waiting for a " to end this parameter
            elif state == 2:
                if c == '\"':
                    state = 0
                    currentlyBuilding = False
                    parameterList.append(currentParameter)
                    currentParameter = str()
                else:
                    currentlyBuilding = True
                    currentParameter = currentParameter + c
            else:
                return None
        # Check to see if we were building a parameter, if so add it to our
        # list
        if currentlyBuilding:
            parameterList.append(currentParameter)
        return parameterList
    
    def ConvertIntoTypes(self, parameterList, toolParameterTypes):
        length = len(parameterList)
        if length != len(toolParameterTypes):
            return None
        for i in range(length):
            if toolParameterTypes[i] == "int":
                try:
                    parameterList[i] = int(parameterList[i])
                except:
                    self.SendParameterError("Unable to convert '" + parameterList[i] + "' to an integer!")
                    return None
            elif toolParameterTypes[i] == "string":
                #it is already a string, so we don't need to do anything
                pass
            elif toolParameterTypes[i] == "float":
                try:
                    parameterList[i] = float(parameterList[i])
                except:
                    self.SendParameterError("Unable to convert '" + parameterList[i] + "' to a float!")
                    return None
            elif toolParameterTypes[i] == "bool":
                try:
                    if parameterList[i].lower() in ['true','t','tru','tr']:
                        parameterList[i] = True
                    elif parameterList[i].lower() in ['false','f','fals','fal']:
                        parameterList[i] = False
                    else:
                        self.SendParameterError("Unable to convert '" + parameterList[i] + "' to a bool!")
                except:
                    self.SendParameterError("Unable to convert '" + parameterList[i] + "' to a bool!")
                    return None
            else:
                self.SendParameterError("The type '" + toolParameterTypes[i] + "' is not recognized by this XTMF Bridge!")
                return None
        return parameterList
    
    def BuildCallString(self, toolName, parameterListName, length):
        string = toolName + "("
        for i in range(length):
            if i > 0:
                string += ','
            string += parameterListName + "[" + str(i) + "]"
        string += ")"
        return string
    
    def SendString(self, stringToSend):
        msg = array.array('u', six.text_type(stringToSend))
        length = len(msg) * msg.itemsize
        tempLength = length
        bytes = 0
        #figure out how many bytes we are going to need to store the length
        #string
        while tempLength > 0:
            tempLength = tempLength >> 7
            bytes += 1
        lengthArray = array.array('B')
        if length <= 0:
            lengthArray.append(0)
        else:
            tempLength = length
            for i in range(bytes):
                current = int(tempLength >> 7)
                current = int(current << 7)
                diff = tempLength - current
                if tempLength < 128:
                    lengthArray.append(diff)
                else:
                    lengthArray.append(diff + 128)
                tempLength = tempLength >> 7
        lengthArray.tofile(self.ToXTMF)
        msg.tofile(self.ToXTMF)
        return
    
    def SendToolDoesNotExistError(self, namespace):
        self.IOLock.acquire()
        self.SendSignal(self.SignalSendToolDoesNotExistsError)
        self.SendString("A tool with the following namespace could not be found: %s" % namespace)
        self.ToXTMF.flush()
        self.IOLock.release()
        return

    def SendParameterError(self, problem):
        self.IOLock.acquire()
        self.SendSignal(self.SignalParameterError)
        self.SendString(problem)
        self.ToXTMF.flush()
        self.IOLock.release()
        return
        
    def SendRuntimeError(self, problem):
        self.IOLock.acquire()
        self.SendSignal(self.SignalRuntimeError)
        self.SendString(problem)
        self.ToXTMF.flush()
        self.IOLock.release()
        return
    
    def SendSuccess(self):
        self.IOLock.acquire()
        intArray = array.array('l')
        intArray.append(self.SignalRunComplete)
        intArray.tofile(self.ToXTMF)
        self.ToXTMF.flush()
        self.IOLock.release()
        return
    
    def SendReturnSuccess(self, returnValue):
        self.IOLock.acquire()
        self.SendSignal(self.SignalRunCompleteWithParameter)
        self.SendString(str(returnValue))
        self.ToXTMF.flush()
        self.IOLock.release()
        return
    
    def SendSignal(self, signal):
        intArray = array.array('l')
        intArray.append(signal)
        intArray.tofile(self.ToXTMF)
        return
    
    def SendPrintSignal(self, stringToPrint):
        self.IOLock.acquire()
        self.SendSignal(self.SignalSendPrintMessage)
        self.SendString(stringToPrint)
        self.ToXTMF.flush()
        self.IOLock.release()
        return

    def ReportProgress(self, progress):
        self.IOLock.acquire()
        self.SendSignal(self.SignalProgressReport)
        floatArray = array.array('f')
        floatArray.append(float(progress))
        floatArray.tofile(self.ToXTMF)
        self.ToXTMF.flush()
        self.IOLock.release()   
        return

    def EnsureModellerToolExists(self, macroName):
        for i in range(1, 10):
            if macroName in self.Modeller.tool_namespaces():       
                return True
            time.sleep(1)
        _m.logbook_write("A tool with the following namespace could not be found: %s" % macroName)
        self.SendToolDoesNotExistError(macroName)
        return False

    def ReorderParametersToMatch(self, toolName, expectedParameterNames, sentParameterNames, parameterList):
        #do a quick check to see if everything is in order
        sizeDifference = len(expectedParameterNames) - len(sentParameterNames)
        if sizeDifference < 0:
            #if the call is using less parameters than expected, then find the
            #parameter we are missing
            missing = []
            for param in sentParameterNames:
                if expectedParameterNames.count(param) == 0:
                    missing.append(param)
            self.SendParameterError(str.join("\r\n", ["Unable to find a parameter in the EMME tool '" + toolName + "' called '" + param + "' that was sent!" for param in missing]))
            return False
        elif sizeDifference > 0:
            #if the call has more parameters than the tool
            missing = []
            for param in expectedParameterNames:
                if sentParameterNames.count(param) == 0:
                    missing.append(param)
            self.SendParameterError(str.join("\r\n", ["A parameter called '" + param + "' was not sent while calling the tool '" + toolName + "'!" for param in missing]))
            return False
        #We know we have the right number of parameters now
        for i in range(0, len(expectedParameterNames)):
            if expectedParameterNames[i] != sentParameterNames[i]:
                count = expectedParameterNames.count(sentParameterNames[i])
                if count == 0:
                    self.SendParameterError("Unable to find a parameter in the EMME tool '" + toolName + "' called '" + sentParameterNames[i] + "'!")
                    return False
                else:
                    index = expectedParameterNames.index(sentParameterNames[i])
                    #then we know there is a miss ordering for this parameter we can just swap
                    temp = sentParameterNames[i]
                    temp2 = parameterList[i]
                    sentParameterNames[i] = sentParameterNames[index]
                    parameterList[i] = parameterList[index]
                    sentParameterNames[index] = temp
                    parameterList[index] = temp2
        return True
    
    def ExecuteModule(self, useBinaryParameters):
        macroName = None
        parameterString = None
        timer = None
        # run the module here
        try:
            #figure out how long the macro's name is
            macroName = self.ReadString()
            if not self.EnsureModellerToolExists(macroName):
                return

            # Read in the parameters from XTMF (This needs to happen first so we don't get out of sync).
            if useBinaryParameters:
                #Read in the number of strings, one for each parameter
                numberOfParameters = int(self.ReadString())
                sentParameterNames = [self.ReadString() for p in range(0, numberOfParameters)]
                parameterList = [self.ReadString() for p in range(0, numberOfParameters)]
            else:
                parameterString = self.ReadString()
                
            
            # Now we can create the tool
            tool = self.CreateTool(macroName)
            toolParameterTypes = self.GetToolParameterTypes(tool)
            if toolParameterTypes == None:
                return

            # Parse the parameters
            if useBinaryParameters:
                expectedParameterNames = self.GetToolParameters(tool)
                if not self.ReorderParametersToMatch(macroName, expectedParameterNames, sentParameterNames, parameterList):
                    return
                parameterString = str.join(',', ['{%s:%s}' %(sentParameterNames[p], parameterList[p]) for p in range(0, numberOfParameters)])
            else:
                parameterList = self.BreakIntoParametersStrings(parameterString)
            
            parameterList = self.ConvertIntoTypes(parameterList, toolParameterTypes)
            if parameterList == None:
                _m.logbook_write("We were unable to create the parameters to their given types, or there was the wrong number of arguments for the tool " + macroName + ".")
                _m.logbook_write("The parameter string was \r\n" + parameterString)
                self.SendParameterError("The module \"" + macroName + "\" was executed with the wrong number of arguments or of invalid types.")
                return
            parameterNames = self.GetToolParameters(tool)
            #Do the exec in another namespace
            nameSpace = {'tool':tool, 'parameterNames':parameterNames, 'parameterList':parameterList}
            for i in range(len(parameterList)):
                if toolParameterTypes[i] == "string":
                    toExecute = "tool." + parameterNames[i] + "='" + str(parameterList[i]).replace("\\","\\\\").replace("'","\\'").replace("\"","\\\"") + "'"
                else:
                    toExecute = "tool." + parameterNames[i] + "=" + str(parameterList[i])
                exec(toExecute, nameSpace, {})
            callString = self.BuildCallString("tool", "parameterList", len(parameterList))
            #Now that everything is ready, attach an instance of ourselves into
            #the tool so they can send progress reports
            tool.XTMFBridge = self
            
            if "percent_completed" in dir(tool):
                timer = ProgressTimer(tool.percent_completed, self)
                timer.start()
            #Execute the tool, getting the return value
            ret = eval(callString, nameSpace, None)
            if timer != None:
                timer.stop()
            
            nameSpace = None
            if ret == None: 
                self.SendSuccess()
            else:
                self.SendReturnSuccess(ret)
        except Exception as inst:
            if timer != None:
                timer.stop()
            _m.logbook_write("We are in the exception code for ExecuteModule")
            if(macroName != None):
                _m.logbook_write("Macro Name: " + macroName)
            else:
                _m.logbook_write("Macro Name: None")
            if(parameterString != None):
                _m.logbook_write("Parameter : " + parameterString)
            else:
                _m.logbook_write("Parameter : None")
            _m.logbook_write(str(inst))

            etype, evalue, etb = sys.exc_info()
            stackList = _traceback.extract_tb(etb)
            msg = "%s: %s\n\nStack trace below:" % (evalue.__class__.__name__, str(evalue))
            stackList.reverse()
            for file, line, func, text in stackList:
                msg += "\n  File '%s', line %s, in %s" % (file, line, func)
            self.SendRuntimeError(msg)
        return
    
    def CleanLogbook(self):
        try:
            projectFile = None
            projectFiles = glob.glob("*.emp")
            if len(projectFiles) > 0:
                projectFile = projectFiles[0]
            if projectFile == None:
                os.chdir("..")
                projectFiles = glob.glob("*.emp")
                if len(projectFiles) > 0:
                    projectFile = projectFiles[0]
            logbookPath = self.Modeller.desktop.modeller_logbook_url
            self.Modeller = None
            self.emmeApplication.close()
            self.emmeApplication = None
            
            time.sleep(10)
            os.remove(logbookPath)
            self.emmeApplication = _app.start_dedicated(visible=False, user_initials="XTMF", project=projectFile)
            self.Modeller = inro.modeller.Modeller(self.emmeApplication)
            self.SendSuccess()
        except Exception as inst:
            self.SendRuntimeError(str(inst))
        return
            
    def SwitchToDatabank(self, emmeApplication, databankName):
        databankName = databankName.lower()
        for db in emmeApplication.data_explorer().databases():
            if db.name().lower() == databankName:
                db.open()
                return
        self.SendRuntimeError("The databank " + databankName + " does not exist!")

    def CheckForMissingTools(self):
        def get_tool_namespace(toolbox, elementIndex):
            element = toolbox.element(elementIndex)
            if element["parent_id"] is None:
                return element["attributes"]["namespace"]
            return get_tool_namespace(toolbox, element["parent_id"]) + "." + element["attributes"]["namespace"]
        
        def does_tool_exist(toolbox, elementIndex):
            element = toolbox.element(elementIndex)
            if element is None:
                return False
            attributes = element["attributes"]
            # If it is a directory the code attribute does not exist
            if not ("code" in attributes):
                return True
            # If code exists and is not None, then the tool exists
            if attributes["code"]:
                return True
            # Check to see if the script exists
            script = attributes["script"]
            if not script:
                raise Exception("There is no file path for the unconsolidated tool " + get_tool_namespace(toolbox, elementIndex) + " defined!")
            if not exists(script):
                raise Exception("The unconsolidated tool \"" + get_tool_namespace(toolbox, elementIndex) + "\" calls a file that does not exist \""+script+"\"!")
            return True

        def read_children(childStr):
            ret = [int(x) for x in childStr[1:len(childStr)-1].split(",")]
            return ret

        def explore_toolbox(toolbox, index):
            element = toolbox.element(index)
            if element is None:
                raise Exception("The given element " + str(index) + " does not exist in the toolbox!")
            if "attributes" not in element:
                raise Exception("The element does not have any attributes " + str(index))
            attributes = element["attributes"]
            if "children" in attributes:
                for childIndex in read_children(attributes["children"]):
                    explore_toolbox(toolbox, childIndex)
            else:
                does_tool_exist(toolbox, index)
        
        for toolbox in self.Modeller.toolboxes:
            try:
                explore_toolbox(toolbox, toolbox.root)
            except Exception as e:
                self.SendRuntimeError(str(e))
                return False
        self.SendSignal(self.SignalRunComplete)
        return True

    def Run(self, performanceMode):
        if performanceMode:
            _m.logbook_write("Performance Testing Activated")
        # now that everything has been redirected we can
        # tell XTMF that we are ready
        self.SendSignal(self.SignalStart)
        self.ToXTMF.flush()
        exit = False
        while(not exit):
            try:
                input = self.ReadInt()
            except  Exception as inst:
                exit = True
                sys.stdout = NullStream()
                # this is because the bridge was closed on the XTMF side
                return
            if input == self.SignalTermination:
                _m.logbook_write("Exiting on termination signal from XTMF")
                exit = True
                sys.stdout = NullStream()
                return
            elif input == self.SignalStartModule:
                if performanceMode:
                    t = timeit.Timer(self.ExecuteModule).timeit(1)
                    _m.logbook_write(str(t) + " seconds to execute.")
                else:
                    self.ExecuteModule(False)
            elif input == self.SignalStartModuleBinaryParameters:
                if performanceMode:
                    t = timeit.Timer(self.ExecuteModule).timeit(1)
                    _m.logbook_write(str(t) + " seconds to execute.")
                else:
                    self.ExecuteModule(True)
            elif input == self.SignalCleanLogbook:
                self.CleanLogbook()
            elif input == self.SignalCheckToolExists:
                self.CheckToolExists()
            elif input == self.SignalDisableLogbook:
                self.DisableLogbook()
            elif input == self.SignalEnableLogbook:
                self.EnableLogbook()
            elif input == self.SignalCheckForMissingTools:
                self.CheckForMissingTools()
            else:
                #If we do not understand what XTMF is saying quietly die
                exit = True
                _m.logbook_write("Exiting on bad input \"" + str(input) + "\"")
                try:
                    self.SendSignal(self.SignalTermination)
                except Exception as e:
                    pass
                sys.stdout = NullStream()
        return

    def CheckToolExists(self):
        ns = self.ReadString()
        ret = ns in self.Modeller.tool_namespaces()
        if ret == False:
            _m.logbook_write("Unable to find a tool named " + ns)
        self.SendReturnSuccess(ret)
        return
    
    def DisableLogbook(self):
        self.previous_level = inro.modeller.logbook_level()
        _m.logbook_level(inro.modeller.LogbookLevel.NONE)
    
    def EnableLogbook(self):
        _m.logbook_level(self.previous_level)
    
#end XTMFBridge

#Get the project file
args = sys.argv # 0: This script's location, 1: Emme project file, 2: User initials, 3:
                # Performance flag
projectFile = args[1]
userInitials = args[2]
performancFlag = bool(int(args[3]))
pipeName = args[4]
databank = None
if len(args) > 5:
    databank = args[5]
#sys.stderr.write(args)
print(userInitials)
print(projectFile)
TheEmmeEnvironmentXMTF = None
try:
    TheEmmeEnvironmentXMTF = _app.start_dedicated(visible=False, user_initials=userInitials, project=projectFile)
except:
    # We can just pass here, if we didn't set the environment then the bridge will terminate
    pass 
    
try:
    XTMFBridge(TheEmmeEnvironmentXMTF, databank,).Run(performancFlag)
except Exception as e:   
    print(dir(e).__class__)
    print(e.message)
    print(e.args)
if TheEmmeEnvironmentXMTF is not None:
    TheEmmeEnvironmentXMTF.close()
