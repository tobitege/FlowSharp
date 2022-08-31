﻿/* The MIT License (MIT)
* 
* Copyright (c) 2015 Marc Clifton
* 
* Permission is hereby granted, free of charge, to any person obtaining a copy
* of this software and associated documentation files (the "Software"), to deal
* in the Software without restriction, including without limitation the rights
* to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
* copies of the Software, and to permit persons to whom the Software is
* furnished to do so, subject to the following conditions:
* 
* The above copyright notice and this permission notice shall be included in all
* copies or substantial portions of the Software.
* 
* THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
* IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
* FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
* AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
* LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
* OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
* SOFTWARE.
*/

using System;

namespace Clifton.Core.Workflow
{
	/// <summary>
	/// A workflow item is a specific process to execute in the workflow.
	/// </summary>
	public class WorkflowItem<T>
	{
		protected Func<WorkflowContinuation<T>, T, WorkflowState> doWork;

		/// <summary>
		/// Instantiate a workflow item.  We take a function that takes the Workflow instance associated with this item
		/// and a data item.  We expect a WorkflowState to be returned.
		/// </summary>
		/// <param name="doWork"></param>
		public WorkflowItem(Func<WorkflowContinuation<T>, T, WorkflowState> doWork)
		{
			this.doWork = doWork;
		}

		/// <summary>
		/// Execute the workflow item method.
		/// </summary>
		public WorkflowState Execute(WorkflowContinuation<T> workflowContinuation, T data)
		{
			return doWork(workflowContinuation, data);
		}
	}
}
