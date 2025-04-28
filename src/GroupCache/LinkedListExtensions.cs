// --------------------------------------------------------------------------------------------------------------------
// <copyright file="LinkedListExtensions.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace GroupCache
{
    using System;
    using System.Collections.Generic;

    public static class LinkedListExtensions
    {
        public static void MoveToFront<T>(this LinkedList<T> list, LinkedListNode<T> node)
        {
            if (node.List != list)
            {
                throw new InvalidOperationException("The provided LinkedList should own the LinkedListNode");
            }

            if (list.First == node)
            {
                return;
            }

            list.Remove(node);
            list.AddFirst(node);
        }
    }
}
