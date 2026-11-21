namespace NexMed360Api.Entities;

public enum PrescriptionStatus { Active, OnHold, Cancelled, Completed, EnteredInError, Stopped, Draft, Unknown }
public enum PrescriptionIntent { Order, Proposal, Plan, OriginalOrder, ReflexOrder, FillerOrder, InstanceOrder, Option }
