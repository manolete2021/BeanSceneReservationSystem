using System.Collections;

namespace BeanSceneReservationSystem.Models;

// Stores reservation IDs in a binary search tree.
public class ReservationBinaryTree : IEnumerable<int>
{
    // The nested Node class stores one value and its child nodes.
    private class Node
    {
        public int ReservationID { get; set; }
        public Node? Left { get; set; }
        public Node? Right { get; set; }

        public Node(int reservationId)
        {
            ReservationID = reservationId;
        }
    }

    private Node? _root;

    // Place a reservation ID to the left or right so the tree stays sorted.
    public void Insert(int reservationId)
    {
        _root = InsertNode(_root, reservationId);
    }

    private static Node InsertNode(Node? current, int reservationId)
    {
        if (current == null)
        {
            return new Node(reservationId);
        }

        if (reservationId < current.ReservationID)
        {
            current.Left = InsertNode(current.Left, reservationId);
        }
        else if (reservationId > current.ReservationID)
        {
            current.Right = InsertNode(current.Right, reservationId);
        }

        return current;
    }

    // Search by going left for a smaller ID and right for a larger ID.
    public bool Search(int reservationId)
    {
        Node? current = _root;

        while (current != null)
        {
            if (reservationId == current.ReservationID)
            {
                return true;
            }

            if (reservationId < current.ReservationID)
            {
                current = current.Left;
            }
            else
            {
                current = current.Right;
            }
        }

        return false;
    }

    // IEnumerable returns the IDs through an in-order walk.
    public IEnumerator<int> GetEnumerator()
    {
        var sortedIds = new List<int>();
        AddInOrder(_root, sortedIds);
        return sortedIds.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    // In-order traversal returns reservation IDs in ascending order.
    private static void AddInOrder(Node? node, List<int> sortedIds)
    {
        if (node == null)
        {
            return;
        }

        AddInOrder(node.Left, sortedIds);
        sortedIds.Add(node.ReservationID);
        AddInOrder(node.Right, sortedIds);
    }
}
