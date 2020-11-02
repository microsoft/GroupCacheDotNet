using System;
using System.Collections.Generic;

namespace GroupCache
{
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
